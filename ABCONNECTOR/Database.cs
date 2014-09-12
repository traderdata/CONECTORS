using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using AmiBroker.Data;
using AmiBroker;
using Traderdata.Client.ABConnector.DTO;
using Traderdata.Client.ABConnector.DAO;
using Traderdata.Client.ABConnector.Util;
using Traderdata.Server.Core.DTO;
using Traderdata.Server.API;


namespace Traderdata.Client.ABConnector
{
    public class Database
    {
        #region private variables

        /// <summary>
        /// Dicionario de tickers
        /// </summary>
        public static Dictionary<string, TickerData> dictionaryTicker = new Dictionary<string, TickerData>();

        /// <summary>
        /// Dicionario que armazena as cotações
        /// </summary>
        public Dictionary<string, QuotationList> dictionaryQuotes = new Dictionary<string, QuotationList>();

        /// <summary>
        /// Lista de thread de processamento
        /// </summary>
        private List<string> processingThreadList = new List<string>();

        /// <summary>
        /// Lista de backfill pendentes
        /// </summary>
        public List<string> pendingBackfill = new List<string>();

        /// <summary>
        /// Timer de controle do status
        /// </summary>
        private System.Timers.Timer timerStatus = new System.Timers.Timer(500);

        /// <summary>
        /// Tempo para dar regfresh no chart
        /// </summary>
        private System.Timers.Timer timerChartRefresh = new System.Timers.Timer(300);

        /// <summary>
        /// Lista de pendencia de assinatura
        /// </summary>
        private List<string> listaPendenteAssinatura = new List<string>();

        /// <summary>
        /// Status do conector
        /// </summary>
        private string status;                     // status vars
        private bool connected = false;

        private Configuration config;               // .NET Data Source's config data (from DatSource.XML file)
        private Workspace workspace;                // AmiBroker's Database settings window
        private Periodicity periodicity;            // db's base time interval (filled at the first call of GetQuotesEx. WorkSpace.TimeBase is not correct in same cases!)
        private Periodicity periodicity2;            // db's base time interval (filled at the first call of GetQuotesEx. WorkSpace.TimeBase is not correct in same cases!)

        private bool firstGetQuotesExCall;


        #endregion

        #region Construtor

        /// <summary>
        /// constructor to save params and init objects
        /// </summary>
        /// <param name="config"></param>
        /// <param name="workSpace"></param>
        public Database(Configuration config, Workspace workSpace)
        {
            timerStatus.Elapsed += new System.Timers.ElapsedEventHandler(timerStatus_Elapsed);
            timerStatus.Start();
            timerChartRefresh.Elapsed += new System.Timers.ElapsedEventHandler(timerChartRefresh_Elapsed);
            timerChartRefresh.Start();
            
            this.config = config;
            this.workspace = workSpace;
        }

        #endregion

        #region Connection and plugin status

        /// <summary>
        /// Metodo que efetua o login
        /// </summary>
        private bool EfetuaLogin()
        {
            try
            {
                //logando no Datafeed
                if (Security.Login("", StaticData.SECURITY_ENDPOINT, StaticData.Login, StaticData.Senha, "ABCONNECTOR"))
                {
                    StaticData.UserHasBMFAccess = Security.User.HasBMFRT;
                    StaticData.UserHasBovespaAccess = Security.User.HasBovespaRT;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception exc)
            {
                return false;
            }
        }

        /// <summary>
        /// Metodo que faz a conexao nos servidores
        /// </summary>
        public void Connect()
        {
            try
            {
                LogAndMessage.LogAndAdd(MessageType.Info, "Iniciando conexao com servidores RT.");

                //devo fazer o login e se for bem sucedido seguir com o procedimento abaixo se nao colocar como offline
                //Iniciando o RealtimeDAO
                StaticData.RealtimeTelnet = null;

                StaticData.RealtimeTelnet = new Server.API.Realtime(false, "BMFBOVESPA", false);
                    
                if (EfetuaLogin())
                {
                    MarketData.InitializeAPI("", StaticData.MARKETDATA_ENDPOINT, Security.User.Guid);
                    MarketDataDAO.InitializeMarketDataChannel();
                    RealTimeDAO.ConnectTelnetServer();
                    RealTimeDAO.TickReceived += new RealTimeDAO.TickHandler(RealtimeCoreBVSP_TickReceived);
                    RealTimeDAO.TradeReceived += new RealTimeDAO.TradeHandler(RealtimeCoreBVSP_NegocioReceived);
                    RealTimeDAO.OnConnect += new RealTimeDAO.OnConnectHandler(RealTimeDAO_OnConnect);

                }
                else
                {
                    //StaticData.StatusConexaoFeed = StaticData.StatusConexao.Offline;
                    status = "Offline";
                }
            }
            catch
            {
                // indicate disconnection
                status = "Offline";
                connected = false;            
            }
            
        }

        /// <summary>
        /// Metodo de desconexao
        /// </summary>
        public void Disconnect()
        {
            // indicate disconnection
            status = "Offline";
            connected = false;
            RealTimeDAO.Disconnect();
        }

        #endregion

        #region API calls

        /// <summary>
        /// Metodo auxiliar que converte um array para QuoteationList
        /// </summary>
        /// <param name="quotes"></param>
        /// <returns></returns>
        private QuotationList ConvertQuotationArrayToQuotationList(QuotationArray quotes)
        {
            QuotationList quoteList = new QuotationList(periodicity);
            for (int i = 0; i < quotes.Count; i++)
            {
                quoteList.Merge(quotes[i]);
            }

            return quoteList;
        }

        /// <summary>
        /// Metodo que solicita ao nosso market data as cotações no formato AMIBroker
        /// </summary>
        /// <param name="ticker"></param>
        /// <param name="quotes"></param>
        public void GetQuotesEx(string ticker, ref QuotationArray quotes)
        {
            try
            {
                // recuperando a periodicidade
                if (!firstGetQuotesExCall)
                {
                    periodicity = quotes.Periodicity;
                    if (quotes.Periodicity == Periodicity.EndOfDay)
                        periodicity2 = Periodicity.EndOfDay;
                    else if (quotes.Periodicity == Periodicity.OneMinute)
                        periodicity2 = Periodicity.OneMinute;
                    else if (quotes.Periodicity == Periodicity.Tick)
                        periodicity2 = Periodicity.Tick;

                    firstGetQuotesExCall = true;
                }

                // assinando os canais em real-time
                SubscribeRealtimeTicker(ticker);

                if (!dictionaryTicker.ContainsKey(ticker))
                {
                    //checando se ja existe no dicionario este ticker
                    RegisterNewTicker(ticker, ConvertQuotationArrayToQuotationList(quotes));
                }

                if (dictionaryTicker[ticker].statusTicker == Status.BackFillRequested)
                {
                    quotes.Clear();
                    dictionaryTicker[ticker].dictionaryQuotes.Clear();
                    dictionaryTicker[ticker].statusTicker = Status.PedingRefreshUpdate;
                }
                else if (dictionaryTicker[ticker].statusTicker == Status.RealtimeReadyToReceiveStreamingUpdates)
                {
                    quotes.Merge(dictionaryTicker[ticker].dictionaryQuotes);
                }
                
            }
            catch (Exception ex)
            {
                LogAndMessage.LogAndAdd(MessageType.Error, "Failed to subscribe to quote update: " + ex);
            }

            
        }

        /// <summary>
        /// Thread que faz o carregamento em background das cotações
        /// </summary>
        /// <param name="ticker"></param>
        public void ThGetQuotesEx(object parameters)
        {
            try
            {
                //carregando as cotações
                List<CotacaoDTO> listaTemp = new List<CotacaoDTO>();
                string ticker = (string)parameters;
                
                if (this.periodicity == Periodicity.Tick)                
                    listaTemp = MarketData.GetTickHistory(ticker, workspace.NumBars);                
                else
                    listaTemp = MarketData.GetIntradayHistory(ticker, workspace.NumBars, 1, false, config.DadoNominal, config.Aftermarket);
                
                
                QuotationList quoteList = new QuotationList(periodicity);
                foreach (CotacaoDTO obj in listaTemp)
                {
                    //get last quote
                    Quotation q = new Quotation();
                    q.DateTime = new AmiDate(obj.Data.ToLocalTime());
                    q.High = (float)obj.Maximo;
                    q.Low = (float)obj.Minimo;
                    q.Open = (float)obj.Abertura;
                    q.Price = (float)obj.Ultimo;
                    q.Volume = (float)obj.Quantidade;
                    quoteList.Merge(q);
                }

                if (!dictionaryQuotes.ContainsKey(ticker))
                    dictionaryQuotes.Add(ticker, quoteList);
                
                processingThreadList.Remove(ticker);
                try
                {
                    if ((this.periodicity == Periodicity.OneMinute) || (this.periodicity == Periodicity.Tick))
                    {
                        RealTimeDAO.AssinaTick(ticker);
                        RealTimeDAO.AssinaTrade(ticker);
                    }
                }
                catch (Exception ex)
                {
                    LogAndMessage.LogAndAdd(MessageType.Error, "Failed to subscribe to real time window update: " + ex);
                }
                DataSourceBase.NotifyQuotesUpdate();


            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        /// <summary>
        /// Solicitação de informações em RT
        /// </summary>
        /// <param name="ticker"></param>
        public void UpdateRecentInfo(string ticker)
        {
            try
            {
                RegisterNewTicker(ticker, new QuotationList(periodicity));
            }
            catch (Exception ex)
            {
                LogAndMessage.LogAndAdd(MessageType.Error, "Failed to subscribe to real time window update: " + ex);
            }
        }

        internal AmiVar GetExtraData(string ticker, string name, Periodicity periodicity, int arraySize)
        {
            return new AmiVar();            
        }

        #endregion

        #region Status Event

        // get connection status to build plugin status
        public bool IsConnected
        {
            get { return status == "C"; }
        }

        // get connection status to build plugin status
        public bool IsDisconnected
        {
            get { return !connected; }
        }

        // get connection status to build plugin status
        public bool IsLoading
        {
            get { return status == "L"; }
        }

        /// <summary>
        /// Timer que verifica o status
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void timerStatus_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (processingThreadList)
            {
                if (connected)
                {
                    if (processingThreadList.Count > 0)
                        status = "L";
                    else
                        status = "C";
                }
                else
                    status = "OFFLINE";
            }
        }

        #endregion


        //novos

        #region Metodos

        /// <summary>
        /// Metodo que faz as assainturas na camada de Realtime
        /// </summary>
        /// <param name="ticker"></param>
        private void SubscribeRealtimeTicker(string ticker)
        {
            //assinando atualização
            if (!RealTimeDAO.listTickSubscription.Contains(ticker))
            {
                if (IsConnected)
                {
                    if ((this.periodicity == Periodicity.OneMinute) || (this.periodicity == Periodicity.Tick))
                    {
                        RealTimeDAO.AssinaTick(ticker);
                        RealTimeDAO.AssinaTrade(ticker);
                    }
                }
            }

        }

        /// <summary>
        /// Metodo que vai fazer o registro do ticker no dicionario
        /// </summary>
        /// <param name="ticker"></param>
        public static void RegisterNewTicker(string ticker, QuotationList quotes)
        {
            if (!dictionaryTicker.ContainsKey(ticker))
            {
                lock (dictionaryTicker)
                {
                    TickerData tickerData = new TickerData();
                    tickerData.ticker = ticker;
                    tickerData.statusTicker = Status.AppendRequested;
                    tickerData.dictionaryQuotes = quotes;
                    dictionaryTicker.Add(ticker, tickerData);
                }
            }
        }

        /// <summary>
        /// Metodo que faz o refresh nas informações do ativo
        /// </summary>
        /// <param name="ticker"></param>
        private void RefreshTickerData(object parameter)
        {
            try
            {
                string ticker = (string)parameter;
                
                
                DateTime? dtDe = null;
                if (dictionaryTicker[ticker].statusTicker == Status.AppendRequested)
                {
                    QuotationList quotes = dictionaryTicker[ticker].dictionaryQuotes;
                    if (quotes.Count > 0)
                        dtDe = new DateTime(quotes[quotes.Count - 1].DateTime.Year, quotes[quotes.Count - 1].DateTime.Month, quotes[quotes.Count - 1].DateTime.Day,
                                                quotes[quotes.Count - 1].DateTime.Hour, quotes[quotes.Count - 1].DateTime.Minute, 0);
                }
                else
                    dtDe = null;

                List<CotacaoDTO> listaTemp = new List<CotacaoDTO>();

                //REVER

                if (this.periodicity == Periodicity.Tick)
                    listaTemp = MarketData.GetTickHistory(ticker, workspace.NumBars);                
                else
                    listaTemp = MarketData.GetIntradayEODMixedHistory(ticker, workspace.NumBars, config.DadoNominal, config.Aftermarket);
                
               
                QuotationList quoteList = new QuotationList(periodicity);
                foreach (CotacaoDTO obj in listaTemp)
                {
                    //get last quote
                    Quotation q = new Quotation();
                    q.DateTime = new AmiDate(obj.Data.ToLocalTime());
                    q.High = (float)obj.Maximo;
                    q.Low = (float)obj.Minimo;
                    q.Open = (float)obj.Abertura;
                    q.Price = (float)obj.Ultimo;
                    q.Volume = (float)obj.Quantidade;
                    q.DateTime.IsEod = obj.IsEOD;                    
                    quoteList.Merge(q);
                }

                //assinando ativos
                SubscribeRealtimeTicker(ticker);

                //atualizando na listagem
                dictionaryTicker[ticker].dictionaryQuotes = quoteList;

                //permitindo que receba dados em RT
                dictionaryTicker[ticker].statusTicker = Status.RealtimeReadyToReceiveStreamingUpdates;

            }
            catch (Exception ex)
            {
                LogAndMessage.LogAndAdd(MessageType.Error, "Failed to refresh quote(" + (string)parameter + "), error:" + ex.ToString());
            }
            
        }

        /// <summary>
        /// Metodo que publica o tick
        /// </summary>
        private void PublishRealtimeInfo(string ativo, bool trade)
        {
            TickDTO tick = dictionaryTicker[ativo].tickInfo;
            if (tick.Ativo == null)
                return;

            // set default RI data
            RecentInfo recentInfo = new RecentInfo();
            recentInfo.Last = (float)dictionaryTicker[tick.Ativo].lastValue;
            recentInfo.Ask = (float)tick.MelhorOfertaVenda;
            recentInfo.AskSize = (int)tick.QuantidadeMelhorOfertaVenda;
            recentInfo.Bid = (float)tick.MelhorOfertaCompra;
            recentInfo.BidSize = (int)tick.QuantidadeMelhorOfertaCompra;

            int lastTickDate = tick.Data.ToLocalTime().Year * 10000 + tick.Data.ToLocalTime().Month * 100 + tick.Data.ToLocalTime().Day;
            int lastTickTime = tick.Data.ToLocalTime().Hour * 10000 + tick.Data.ToLocalTime().Minute * 100 + tick.Data.ToLocalTime().Second;

            recentInfo.DateChange = lastTickDate;
            recentInfo.TimeChange = lastTickTime;
            recentInfo.DateUpdate = lastTickDate;
            recentInfo.TimeUpdate = lastTickTime;

            recentInfo.Change = (float)dictionaryTicker[tick.Ativo].lastValue - (float)tick.FechamentoAnterior;
            recentInfo.High = (float)tick.Maximo;
            recentInfo.Low = (float)tick.Minimo;
            recentInfo.Open = (float)tick.Abertura;
            recentInfo.Prev = (float)tick.FechamentoAnterior;
            recentInfo.iTradeVol = (int)tick.QuantidadeUltimoNegocio;
            recentInfo.iTotalVol = (int)tick.Quantidade;
            recentInfo.Name = ativo;

            recentInfo.Status |= RecentInfoStatus.Update  | RecentInfoStatus.BidAsk;
            if (trade)
                recentInfo.Status |= RecentInfoStatus.Trade;

            recentInfo.Bitmap = RecentInfoField.Ask | RecentInfoField.Bid |
                RecentInfoField.DateChange | RecentInfoField.DateUpdate |
                RecentInfoField.HighLow | RecentInfoField.Last |
                RecentInfoField.None | RecentInfoField.Open |
                RecentInfoField.OpenInt | RecentInfoField.PrevChange |
                RecentInfoField.Shares | RecentInfoField.TotalVol |
                RecentInfoField.TradeVol | RecentInfoField.Week52;


            DataSourceBase.NotifyRecentInfoUpdate(tick.Ativo, ref recentInfo);


        }

        #endregion

        #region Realtime Events

        /// <summary>
        /// Evento disparado a cada trade executado
        /// </summary>
        /// <param name="negocio"></param>
        void RealtimeCoreBVSP_NegocioReceived(NegocioDTO negocio)
        {
            if (this.periodicity == Periodicity.EndOfDay)
                return;

            //se for um cancelamento de negocio
            if (negocio.TipoRegistro != "N")
                return;

            // set default RI data
            if (dictionaryTicker.ContainsKey(negocio.Ativo))
            {
                if (dictionaryTicker[negocio.Ativo].statusTicker == Status.RealtimeReadyToReceiveStreamingUpdates)
                {

                    dictionaryTicker[negocio.Ativo].lastValue = negocio.Valor;

                    if (this.periodicity2 == Periodicity.OneMinute)
                    {
                        QuotationList quotes = dictionaryTicker[negocio.Ativo].dictionaryQuotes;

                        if ((quotes.Count > 0) && ((quotes[quotes.Count - 1].DateTime.Hour == negocio.Data.Hour) && (quotes[quotes.Count - 1].DateTime.Minute == negocio.Data.Minute)))
                        {
                            Quotation quote = new Quotation();
                            quote = quotes[quotes.Count - 1];
                            quote.Price = (float)negocio.Valor;
                            quote.Low = (float)Math.Min(quotes[quotes.Count - 1].Low, negocio.Valor);
                            quote.High = (float)Math.Max(quotes[quotes.Count - 1].High, negocio.Valor);
                            quote.Volume += (float)negocio.Quantidade;
                            quotes[quotes.Count - 1] = quote;
                        }
                        else
                        {
                            Quotation quote = new Quotation();
                            quote.Open = (float)negocio.Valor;
                            quote.DateTime = new AmiDate(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day,
                                negocio.Data.ToLocalTime().Hour, negocio.Data.ToLocalTime().Minute, 59));
                            quote.High = (float)negocio.Valor;
                            quote.Low = (float)negocio.Valor;
                            quote.Price = (float)negocio.Valor;
                            quote.Volume = (float)negocio.Quantidade;
                            dictionaryTicker[negocio.Ativo].dictionaryQuotes.Add(quote);
                        }
                    }
                    else if (this.periodicity2 == Periodicity.Tick)
                    {
                        QuotationList quotes = dictionaryTicker[negocio.Ativo].dictionaryQuotes;

                        Quotation quote = new Quotation();
                        quote.Open = (float)negocio.Valor;
                        quote.DateTime = new AmiDate(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day,
                            negocio.Data.ToLocalTime().Hour, negocio.Data.ToLocalTime().Minute, negocio.Data.ToLocalTime().Second, negocio.Data.ToLocalTime().Millisecond));
                        quote.High = (float)negocio.Valor;
                        quote.Low = (float)negocio.Valor;
                        quote.Price = (float)negocio.Valor;
                        quote.Volume = (float)negocio.Quantidade;
                        dictionaryTicker[negocio.Ativo].dictionaryQuotes.Add(quote);
                        
                    }
                }
            }


            //publicando informação
            PublishRealtimeInfo(negocio.Ativo, true);
        }

        /// <summary>
        /// Evento disparado no update do tick
        /// </summary>
        /// <param name="tick"></param>
        void RealtimeCoreBVSP_TickReceived(TickDTO tick)
        {
            if ((dictionaryTicker.ContainsKey(tick.Ativo)) && (dictionaryTicker[tick.Ativo].lastValue == 0))
                dictionaryTicker[tick.Ativo].lastValue = tick.Ultimo;
            else
                return;

            if (MarketDataDAO.IsIndiceBovespa(tick.Ativo))
                dictionaryTicker[tick.Ativo].lastValue = tick.Ultimo;

            //alterando o last tick
            dictionaryTicker[tick.Ativo].tickInfo = tick;

            //publishing info
            PublishRealtimeInfo(tick.Ativo, false);
        }

        /// <summary>
        /// Faz o refresh no chart
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void timerChartRefresh_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsConnected)
            {
                lock (dictionaryTicker)
                {
                    foreach (string objKey in dictionaryTicker.Keys)
                    {
                        SubscribeRealtimeTicker(objKey);

                        switch (dictionaryTicker[objKey].statusTicker)
                        {
                            case Status.AppendRequested:
                                Thread thRequest = new Thread(new ParameterizedThreadStart(RefreshTickerData));
                                thRequest.Start(objKey);
                                RefreshTickerData(objKey);                                
                                break;
                            case Status.PedingRefreshUpdate:
                                RefreshTickerData(objKey);
                                //dictionaryTicker[objKey].statusTicker = Status.RealtimeReadyToReceiveStreamingUpdates;
                                break;
                        }
                    }
                }
            }

            //soliictando atualização do gráfico
            DataSourceBase.NotifyQuotesUpdate();
        }

        /// <summary>
        /// Evento de conexao no canal em RT
        /// </summary>
        void RealTimeDAO_OnConnect()
        {         
            //StaticData.StatusConexaoFeed = StaticData.StatusConexao.Realtime;
            //indicate "successfull" connection
            status = "C";
            connected = true;
            foreach (string obj in dictionaryTicker.Keys)
            {
                dictionaryTicker[obj].statusTicker = Status.BackFillRequested;
            }
            RealTimeDAO.listTickSubscription.Clear();
        }

        //void RealtimeCore_OnDisconnect()
        //{
        //    // indicate disconnection
        //    status = "Offline";
        //    connected = false;
        //    StaticData.RealtimeCore.FechaConexao();
        //    StaticData.RealtimeCore.OnDisconnect -= new Client.DataFeedNetConnect.Core.DisconnectHandler(RealtimeCore_OnDisconnect);
        //    //StaticData.RealtimeCore = null;
        //}

        #endregion
    }
}
