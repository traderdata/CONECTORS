using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SDK_MS_NET;
using System.Timers;
using System.Configuration;
using Traderdata.Server.Core.DTO;
using Traderdata.Server.API;
using Traderdata.Client.eSginalConnector.DAO;

namespace Traderdata.Client.eSginalConnector
{
    public class eSignalServer: IMetaStockRequest
    {
        #region Variaveis

        /// <summary>
        /// Tempo para verificação de demanda do easignal
        /// </summary>
        private Timer timerRequest = new Timer(400);

        /// <summary>
        /// Dicionario onde ficam armazenados os ultimos ticks
        /// </summary>
        private Dictionary<string, TickDTO> dictionaryTick = new Dictionary<string, TickDTO>();

        /// <summary>
        /// Objeto usado para controlar Lock eSignal
        /// </summary>
        private object SyncRootRequest = new object();

        /// <summary>
        /// Objeto usado para lockar o envio de dados em tempo real, basicamente ele impede que um novo ativo seja assinado enquanto
        /// está sendo enviado dado em real time.
        /// </summary>
        private object SyncRootRt = new object();

        /// <summary>
        /// Dicionario que armazena as solicitações feitas ao servidor eSignal
        /// </summary>
        private Dictionary<int, sRequest> requestMap = new Dictionary<int, sRequest>();

        /// <summary>
        /// Dicionario que controla quais ativos estao assinados em tempo real e seus Ids de Request
        /// </summary>
        private Dictionary<int, sRequest> rtMap = new Dictionary<int, sRequest>();

        /// <summary>
        /// Wrapper da eSignal
        /// </summary>
        private SdkWrapper wrapperEsignal;

        #endregion

        #region Construtor

        /// <summary>
        /// Construtor padrão
        /// </summary>
        public eSignalServer()
        {
            
            //Iniciando o wrapper
            wrapperEsignal = new SdkWrapper(this);

            //Iniciando o metastock
            wrapperEsignal.initialize();

            //iniciando o timer
            timerRequest.Elapsed += new ElapsedEventHandler(timerRequest_Elapsed);
            timerRequest.Start();

            //assinando eventos de Realtime na camada de marketdata      
            RealTimeDAO.TickReceived += new RealTimeDAO.TickHandler(RealTimeDAO_TickReceived);

            //assinando eventos de realtime para comandos de trades
            RealTimeDAO.TradeReceived += new RealTimeDAO.TradeHandler(RealTimeDAO_TradeReceived);
            
        }

        
                

        #endregion

        #region Metodos/Eventos eSignalServer

        /// <summary>
        /// Evento disparado para checar se existe alguma solicitação ainda nao atendida 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void timerRequest_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (SyncRootRequest)
                {
                    foreach (var item in requestMap)
                    {
                        switch (item.Value.requestType)
                        {
                            #region Realtime
                            case eRequestType.eRealTime:
                                {
                                    lock (SyncRootRt)
                                    {
                                        if (item.Value.bAdvise)
                                        {
                                            rtMap[item.Key] = item.Value;
                                        }
                                        else
                                        {
                                            if (rtMap.ContainsKey(item.Key))
                                            {
                                                rtMap.Remove(item.Key);
                                            }
                                        }
                                    }
                                }
                                break;
                            #endregion

                            #region Tick
                            case eRequestType.eTick:
                                //Lista auxiliar que será enviada ao Metastock
                                List<Bar> dataTick = new List<Bar>();

                                    //Populando a lista que será devolvida ao metastock
                                    StaticData.LogEvent("Solicitando dados de " + item.Value.symbol + " ao servidor de TickData. Dados solicitados desde " + item.Value.start.ToString());
                                    foreach (NegocioDTO obj in MarketDataDAO.GetTrades(item.Value.symbol, 50000))
                                    {
                                        Bar bar = new Bar();

                                        bar.time = obj.Data;
                                        bar.close = obj.Valor;
                                        //bar.open = obj.Valor;
                                        //bar.low = obj.Valor;
                                        //bar.high = obj.Valor;
                                        bar.volume = Convert.ToInt32(obj.Quantidade);
                                        dataTick.Add(bar);
                                    }

                                    //Enviando ao Meta
                                    if (dataTick.Count > 0)
                                        wrapperEsignal.history_tick(item.Key, item.Value.symbol, dataTick);
                                    else
                                        wrapperEsignal.history_tick(item.Key, item.Value.symbol, RetornaBarraVazia());
                                
                                break;
                            #endregion

                            #region eMinutes
                            case eRequestType.eMinutes:
                                {
                                    //Lista auxiliar que será enviada ao Metastock
                                    List<Bar> data = new List<Bar>();

                                    //variavel de controle se e indice
                                    bool isIndice = false;

                                    //checando se é indice
                                    isIndice = MarketDataDAO.IsIndiceBovespa(item.Value.symbol);

                                    //Populando a lista que será devolvida ao metastock
                                    StaticData.LogEvent("Solicitando dados de " + item.Value.symbol + " ao servidor de IntradayHistoryData. Dados solicitados desde " + item.Value.start.ToString());
                                    foreach (CotacaoDTO obj in MarketDataDAO.GetCotacaoIntraday(item.Value.symbol, 
                                        item.Value.start, DateTime.MaxValue))
                                    {
                                        Bar bar = new Bar();

                                        bar.time = obj.Data;
                                        
                                        bar.close = obj.Ultimo;
                                        bar.open = obj.Abertura;
                                        bar.low = obj.Minimo;
                                        bar.high = obj.Maximo;
                                        if (!isIndice)
                                            bar.volume = Convert.ToInt32(obj.Quantidade);
                                        else
                                            bar.volume = Convert.ToInt32(obj.Volume/1000);
                                        data.Add(bar);
                                    }

                                    //Enviando ao Meta
                                    if (data.Count > 0)
                                        wrapperEsignal.history_minutes(item.Key, item.Value.symbol, item.Value.iPeriodicity, data);
                                    else
                                    {
                                        if (ConfigurationSettings.AppSettings.AllKeys.Contains("INTEGRATED-SOFTWARE"))
                                        {
                                            if (ConfigurationSettings.AppSettings["INTEGRATED-SOFTWARE"] == "METASTOCK")
                                            {
                                                wrapperEsignal.history_day(item.Key, item.Value.symbol, RetornaBarraVazia());
                                            }
                                        }
                                    }


                                    
   
                                }
                                break;
                            #endregion

                            #region eDaily
                            case eRequestType.eDaily:
                                {
                                    //Lista auxiliar que será enviada ao Metastock
                                    List<Bar> data = new List<Bar>();

                                    //descobrindo qual a bolsa
                                    bool divideQuantidade = false;
                                    bool isIndice = false;

                                    //checando se é indice
                                    isIndice = MarketDataDAO.IsIndiceBovespa(item.Value.symbol);
                                
                                    //carregando as cotações
                                    StaticData.LogEvent("Solicitando dados de " + item.Value.symbol + " ao servidor de EODHistoryData. Dados solicitados desde " + item.Value.start.ToString());
                                    List<CotacaoDTO> listaTemp = MarketDataDAO.GetCotacaoDiaria(item.Value.symbol, DateTime.MaxValue);

                                    //checando se é necessario se dividir as quantidade
                                    foreach (CotacaoDTO obj in listaTemp)
                                    {
                                        int quantidade = 0;
                                        if (!Int32.TryParse(obj.Quantidade.ToString(), out quantidade))
                                        {
                                            divideQuantidade = true;
                                            continue;
                                        }

                                    }

                                    //Populando a lista que será devolvida ao metastock
                                    foreach (CotacaoDTO obj in listaTemp)
                                    {
                                        Bar bar = new Bar();
                                        bar.time = new DateTime(obj.Data.Year, obj.Data.Month, obj.Data.Day, 18, 0, 0);
                                        bar.close = obj.Ultimo;
                                        bar.open = obj.Abertura;
                                        bar.low = obj.Minimo;
                                        bar.high = obj.Maximo;

                                        if (!isIndice)
                                        {
                                            if (divideQuantidade)
                                                bar.volume = Convert.ToInt32(obj.Quantidade / 1000);
                                            else
                                                bar.volume = Convert.ToInt32(obj.Quantidade);
                                        }
                                        else
                                            bar.volume = Convert.ToInt32(obj.Volume / 1000);

                                        data.Add(bar);
                                    }
                                    
                                    //enviando para o eSignalServer
                                    if (data.Count > 0)
                                        wrapperEsignal.history_day(item.Key, item.Value.symbol, data);
                                    else
                                    {
                                        if (ConfigurationSettings.AppSettings.AllKeys.Contains("INTEGRATED-SOFTWARE"))
                                        {
                                            if (ConfigurationSettings.AppSettings["INTEGRATED-SOFTWARE"] == "METASTOCK")
                                            {
                                                wrapperEsignal.history_day(item.Key, item.Value.symbol, RetornaBarraVazia());
                                            }
                                        }
                                    }
                                    
                                }
                                break;

                            #endregion

                        }                         
                    }

                    requestMap.Clear();
                }
            }                 
            catch (Exception exc)
            {
                throw exc;
            }
        }

        /// <summary>
        /// Metodo que desassina o ativo
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        public void RequestRealTimeStop(Int32 iRequestId, String Symbol)
        {
            sRequest request = new sRequest();
            request.symbol = Symbol;
            request.requestType = eRequestType.eRealTime;
            request.bAdvise = false;
            request.entregarDado = true;

            lock (SyncRootRequest)
            {
                requestMap[iRequestId] = request;
            }

        }

        /// <summary>
        /// Metodo que faz a assinatura do ativo
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        public void RequestRealTimeStart(Int32 iRequestId, String Symbol)
        {   
            //Montando a requisição eSignal
            sRequest request = new sRequest();
            request.symbol = Symbol;
            request.requestType = eRequestType.eRealTime;
            request.bAdvise = true;
            request.entregarDado = true;

            lock (SyncRootRequest)
            {
                requestMap[iRequestId] = request;
            }

            lock (SyncRootRt)
            {
                rtMap[iRequestId] = request;               
            }

            //Assinando o ativo
            RealTimeDAO.AssinaTick(Symbol);
            RealTimeDAO.AssinaTrade(Symbol);
        }

        /// <summary>
        /// Metodo que requer o historico em tick
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryTick(Int32 iRequestId, String Symbol, DateTime start, DateTime stop)
        {            
            sRequest request = new sRequest();
            request.symbol = Symbol;
            request.requestType = eRequestType.eTick;
            request.entregarDado = true;
            request.start = start;
            request.stop = stop;
            
            lock (SyncRootRequest)
            {
                requestMap[iRequestId] = request;
            }

        }

        /// <summary>
        /// Metodo que faz a requisição do hsitorico em minutos
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryMinute(Int32 iRequestId, String Symbol, DateTime start, DateTime stop)
        {
            sRequest request = new sRequest();
            request.symbol = Symbol;
            request.requestType = eRequestType.eMinutes;
            request.entregarDado = true;
            request.start = start;
            request.stop = stop;
            
            lock (SyncRootRequest)
            {
                requestMap[iRequestId] = request;
            }

        }

        /// <summary>
        /// Requisição de historico em n minutos
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="iMinutes"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryMinutes(Int32 iRequestId, String Symbol, Int32 iMinutes, DateTime start, DateTime stop)
        {
            //MessageBox.Show(Symbol);
        }

        /// <summary>
        /// Requisição de historico em dia
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryDay(Int32 iRequestId, String Symbol, DateTime start, DateTime stop)
        {
            sRequest request = new sRequest();
            request.symbol = Symbol;
            request.requestType = eRequestType.eDaily;
            request.entregarDado = true;

            request.start = start;
            request.stop = stop;
            
            lock (SyncRootRequest)
            {
                requestMap[iRequestId] = request;
            }

        }

        /// <summary>
        /// Requsiiçãio de historico por semana
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryWeek(Int32 iRequestId, String Symbol, DateTime start, DateTime stop)
        {

        }

        /// <summary>
        /// Requisição de historico em meses
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryMonth(Int32 iRequestId, String Symbol, DateTime start, DateTime stop)
        {


            sRequest request = new sRequest();
            request.symbol = Symbol;
            request.requestType = eRequestType.eMonthly;
            request.entregarDado = true;

            request.start = start;
            request.stop = stop;

            lock (SyncRootRequest)
            {
                requestMap[iRequestId] = request;
            }

        }

        /// <summary>
        /// Requisição de historico em anos
        /// </summary>
        /// <param name="iRequestId"></param>
        /// <param name="Symbol"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        public void RequestHistoryYear(Int32 iRequestId, String Symbol, DateTime start, DateTime stop)
        {

        }

        #endregion

        #region Metodos

        /// <summary>
        /// Metodo que monta a lista de ativos
        /// </summary>
        private void MontaArquivoDeAtivos()
        {
            //try
            //{
            //    //Aqui está conectado devo então solicitar os ativos
            //    List<AtivoDTO> listaAtivo = MemoryData.AtivosTodos;
            //    List<string> listaAtivoIbov = historico.GetAtivosIBOV();

            //    //Montando Sym para Bovespa
            //    using (StreamWriter writer = new StreamWriter("tempSymBovespa.sym"))
            //    {
            //        foreach (AtivoDTO obj in listaAtivo)
            //        {
            //            if (obj.Bolsa == EnumLocal.Bolsa.Bovespa)
            //                writer.WriteLine(obj.Empresa + "(" + obj.Ativo + ")," + obj.Ativo
            //                    + "," + "BOVESPA,,0000,0000");
            //        }
            //    }
            //    //Montando Sym para IBOV
            //    using (StreamWriter writer = new StreamWriter("tempSymIBOV.sym"))
            //    {
            //        foreach (string ativo in listaAtivoIbov)
            //        {
            //            AtivoDTO obj = historico.GetAtivo(ativo);
            //            if (obj != null)
            //                writer.WriteLine(obj.Empresa + "(" + obj.Ativo + ")," + obj.Ativo
            //                    + "," + "BOVESPA,,0000,0000");
            //        }
            //    }
            //    //Montando Sym para BMF
            //    using (StreamWriter writer = new StreamWriter("tempSymBMF.sym"))
            //    {
            //        foreach (AtivoDTO obj in listaAtivo)
            //        {
            //            if (obj.Bolsa == EnumLocal.Bolsa.BMF)
            //                writer.WriteLine(obj.Empresa + "(" + obj.Ativo + ")," + obj.Ativo
            //                    + "," + "BMF,,0000,0000");
            //        }
            //    }

            //    //Agora devo copiar os arquivos para dentro das pastas do metastock

            //    RegistryKey rk = Registry.CurrentUser.OpenSubKey("Software\\Equis\\", false);
            //    string[] chaves = rk.GetSubKeyNames();
            //    foreach (string obj in chaves)
            //    {
            //        if (obj.Contains("DownLoader"))
            //        {
            //            //Nesse caso devo pegar o diretorio
            //            RegistryKey rkDir = rk.OpenSubKey(obj);
            //            string[] chavesTheDownloader = rkDir.GetSubKeyNames();
            //            foreach (string strDir in chavesTheDownloader)
            //            {
            //                if (strDir == "File Paths")
            //                {
            //                    RegistryKey rkfilePath = rkDir.OpenSubKey(strDir);
            //                    string[] chaveThefilePath = rkfilePath.GetValueNames();

            //                    foreach (string strFinal in chaveThefilePath)
            //                    {
            //                        if (strFinal == "DownLoader")
            //                        {
            //                            string diretorio = Convert.ToString(rkfilePath.GetValue(strFinal));
            //                            //Copiar para o diretorio
            //                            File.Copy("tempSymBovespa.sym", diretorio + "\\Traderdata-Bovespa.sym", true);
            //                            File.Copy("tempSymBMF.sym", diretorio + "\\Traderdata-BMF.sym", true);
            //                            File.Copy("tempSymIBOV.sym", diretorio + "\\Traderdata-IBOV.sym", true);

            //                        }
            //                    }

            //                }
            //            }
            //        }

            //    }
            //}
            //catch
            //{
            //    MessageBox.Show("O windows não permitiu que os arquivos fossem copiados. Por favor verifique suas configurações de UAC ou entre em contato com a central de suporte.");                
            //}
        }

        /// <summary>
        /// Metodo que retorna barra vazia
        /// </summary>
        /// <returns></returns>
        public List<Bar> RetornaBarraVazia()
        {
            List<Bar> lista = new List<Bar>();
            Bar dataBar2 = new Bar();
            dataBar2.close = 0;
            dataBar2.high = 0;
            dataBar2.low = 0;
            dataBar2.open = 0;
            dataBar2.time = DateTime.Today;
            dataBar2.volume = 0;
            lista.Add(dataBar2);
            return lista;
        }
        

        #endregion

        #region Eventos

    
        /// <summary>
        /// Evento disparado quando se recebe um negocio
        /// </summary>
        /// <param name="negocio"></param>
        void RealTimeDAO_TradeReceived(object Result)
        {
            NegocioDTO negocio = (NegocioDTO)Result;
        
            if ( (negocio.Valor == 0) || (negocio.TipoRegistro != "N"))
                return;

            //pegando o ultimo tick armazenado
            TickDTO tickAux = new TickDTO();
            int quantidadeDiaria = 0;

            if (dictionaryTick.TryGetValue(negocio.Ativo, out tickAux))
                quantidadeDiaria = Convert.ToInt32(tickAux.Quantidade);
            else
                quantidadeDiaria = 0;


            //lista de ticks que vieram no pacote
            List<Tick> rt = new List<Tick>();

            //DateTime timeAgora = new DateTime();
            //if (!negocio.Pushed)
            //{
             //   timeAgora = new DateTime(negocio.Data.Year, negocio.Data.Month, negocio.Data.Day, negocio.Data.Hour, negocio.Data.Minute, negocio.Data.Second);
            //}
            //else
            //{
            //    timeAgora = new DateTime(negocio.DataHora.Year, negocio.DataHora.Month, negocio.DataHora.Day,
            //        Convert.ToInt32(negocio.HoraBolsa.Substring(0, 2)) + StaticData.UtcExchangeTimeDifference,
            //        Convert.ToInt32(negocio.HoraBolsa.Substring(2, 2)),
            //        Convert.ToInt32(negocio.HoraBolsa.Substring(4, 2)));
            //}
            //tratando Quantidade
            Tick Quantidade = new Tick();
            Quantidade.eFieldId = eTickType.TRADEVOLUME;
            Quantidade.time = negocio.Data;
            Quantidade.value = negocio.Quantidade;
            rt.Add(Quantidade);

            //tratando Ultimo
            Tick Ultimo = new Tick();
            Ultimo.eFieldId = eTickType.LAST;
            Ultimo.time = negocio.Data;
            Ultimo.value = negocio.Valor;
            rt.Add(Ultimo);

            //tratando Variacao
            Tick Volume = new Tick();
            Volume.eFieldId = eTickType.TOTALVOLUME;
            Volume.time = negocio.Data;
            Volume.value = quantidadeDiaria;
            rt.Add(Volume);

            //enviando para o servidor eSignal
            lock (SyncRootRt)
            {
                foreach (KeyValuePair<int, sRequest> obj in rtMap.ToList())
                {
                    if (negocio.Ativo == obj.Value.symbol)
                        wrapperEsignal.realtime(obj.Key, obj.Value.symbol, rt);             
                }
            }
        }

        /// <summary>
        /// Evento de atualização de ticks
        /// </summary>
        /// <param name="tick"></param>
        void RealTimeDAO_TickReceived(object Result)
        {
            TickDTO tick = (TickDTO)Result;
            //se for o tick = a 0 deve dar return
            if (tick.Ultimo == 0)
                return;

            //armazenado o tick
            if (dictionaryTick.ContainsKey(tick.Ativo))
                dictionaryTick[tick.Ativo] = tick;
            else
                dictionaryTick.Add(tick.Ativo, tick);


            //lista de ticks que vieram no pacote
            List<Tick> rt = new List<Tick>();

            //DateTime timeAgora = new DateTime(tick.Data.Year, tick.Data.Month, tick.Data.Day,
            //        tick.Data.Hour, tick.Data.Minute, tick.Data.Second);

            Tick Abertura = new Tick();
            Tick Maximo = new Tick();
            Tick Minimo = new Tick();
            Tick Ultimo = new Tick();
            Tick FechamentoAnterior = new Tick();
            Tick MelhorOfertaCompra = new Tick();
            Tick MelhorOfertaVenda = new Tick();
            Tick Quantidade = new Tick();
            Tick QuantidadeMelhorOfertaCompra = new Tick();
            Tick QuantidadeMelhorOfertaVenda = new Tick();
            Tick Variacao = new Tick();
            Tick Volume = new Tick();

            //tratando Abertura
            Abertura.eFieldId = eTickType.DAILY_OPEN;
            Abertura.time = tick.Data;
            Abertura.value = tick.Abertura;
            rt.Add(Abertura);

            //tratando Maximo
            Maximo.eFieldId = eTickType.DAILY_HIGH;
            Maximo.time = tick.Data;
            Maximo.value = tick.Maximo;
            rt.Add(Maximo);

            //tratando Minimo
            Minimo.eFieldId = eTickType.DAILY_LOW;
            Minimo.time = tick.Data;
            Minimo.value = tick.Minimo;
            rt.Add(Minimo);

            //tratando Fechamento Anterior
            FechamentoAnterior.eFieldId = eTickType.PREV_CLOSE;
            FechamentoAnterior.time = tick.Data;
            FechamentoAnterior.value = tick.FechamentoAnterior;
            rt.Add(FechamentoAnterior);

            //tratando MelhorOfertaCompra
            MelhorOfertaCompra.eFieldId = eTickType.BID;
            MelhorOfertaCompra.time = tick.Data;
            MelhorOfertaCompra.value = tick.MelhorOfertaCompra;
            rt.Add(MelhorOfertaCompra);

            //tratando MelhorOfertaCompra
            QuantidadeMelhorOfertaCompra.eFieldId = eTickType.BIDSIZE;
            QuantidadeMelhorOfertaCompra.time = tick.Data;
            QuantidadeMelhorOfertaCompra.value = tick.QuantidadeMelhorOfertaCompra;
            rt.Add(QuantidadeMelhorOfertaCompra);

            //tratando MelhorOfertaVenda
            MelhorOfertaVenda.eFieldId = eTickType.ASK;
            MelhorOfertaVenda.time = tick.Data;
            MelhorOfertaVenda.value = tick.MelhorOfertaVenda;
            rt.Add(MelhorOfertaVenda);

            //tratando QuantidadeMelhorOfertaVenda
            QuantidadeMelhorOfertaVenda.eFieldId = eTickType.ASKSIZE;
            QuantidadeMelhorOfertaVenda.time = tick.Data;
            QuantidadeMelhorOfertaVenda.value = tick.QuantidadeMelhorOfertaVenda;
            rt.Add(QuantidadeMelhorOfertaVenda);


            
            //tratamento especial para indices que devem receber o volume imposto e o ultimo a partir de tick mesmo
            if (MarketDataDAO.IsIndiceBovespa(tick.Ativo))
            {
                //tratando Variacao
                Volume.eFieldId = eTickType.TOTALVOLUME;
                Volume.time = tick.Data;
                Volume.value = tick.Volume;
                rt.Add(Volume);

                //tratando Ultimo
                Ultimo.eFieldId = eTickType.LAST;
                Ultimo.time = tick.Data;
                Ultimo.value = tick.Ultimo;
                rt.Add(Ultimo);
            }

            ////tratamento especial para BMF em caso de problemas no UMDF
            //if (StaticData.ConfiguracaoGeral.BMFFeedServer == "B")
            //{
            //    if (MarketDataDAO.IsBMF(tick.Ativo))
            //    {
            //        //tratando Variacao
            //        Volume.eFieldId = eTickType.TOTALVOLUME;
            //        Volume.time = timeAgora;
            //        Volume.value = tick.Volume;
            //        rt.Add(Volume);

            //        //tratando Ultimo
            //        Ultimo.eFieldId = eTickType.LAST;
            //        Ultimo.time = timeAgora;
            //        Ultimo.value = tick.Ultimo;
            //        rt.Add(Ultimo);
            //    }
            //}


            //enviando para o servidor eSignal
            lock (SyncRootRt)
            {
                foreach (KeyValuePair<int, sRequest> obj in rtMap.ToList())
                {
                    if (tick.Ativo == obj.Value.symbol)
                        wrapperEsignal.realtime(obj.Key, obj.Value.symbol, rt);
                }
            }


        }

        #endregion

    }

    #region Enums Auxiliares

    /// <summary>
    /// Enum que identifica o tipo de solicitação qeu está sendo feita
    /// </summary>
    public enum eRequestType
    {
        eRealTime = 0,
        eTick,
        eMinutes,
        eDaily,
        eWeekly,
        eMonthly,
        eYearly
    };

    /// <summary>
    /// Estrutura que identifica uma solicitação ao servidor eSginal
    /// </summary>
    public struct sRequest
    {
        public string symbol;
        public eRequestType requestType;
        public int iPeriodicity;
        public bool bAdvise;
        public DateTime start;
        public DateTime stop;
        public bool entregarDado;
    };

    #endregion
}
