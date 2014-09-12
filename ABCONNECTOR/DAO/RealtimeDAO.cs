using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Traderdata.Server.Core.DTO;
using Traderdata.Server.API;
using System.Net.Sockets;

namespace Traderdata.Client.ABConnector.DAO
{
    public static class RealTimeDAO
    {
        #region Variaveis Privadas
                
        /// <summary>
        /// Variavel que armazena os ultimos ticks
        /// </summary>
        private static Dictionary<string, TickDTO> dictionaryTicks = new Dictionary<string, TickDTO>();

        /// <summary>
        /// Lista de assinaturas de tick
        /// </summary>
        public static List<string> listTickSubscription = new List<string>();

        /// <summary>
        /// Lista de assinaturas de trades
        /// </summary>
        public static List<string> listTradeSubscription = new List<string>();

        #endregion

        #region Eventos

        /// <summary>
        /// Representa o método que irá manipular o evento de recebimento de tick.
        /// </summary>
        /// <param name="tick"></param>
        public delegate void TickHandler(TickDTO Result);

        /// <summary>Evento disparado quando a ação de StartTickSubscription é executada.</summary>
        public static event TickHandler TickReceived;

        /// <summary>
        /// Representa o método que irá manipular o evento de recebimento de trade.
        /// </summary>
        /// <param name="tick"></param>
        public delegate void TradeHandler(NegocioDTO Result);

        /// <summary>Evento disparado quando de recebimento de yrade é executada.</summary>
        public static event TradeHandler TradeReceived;

        /// <summary>
        /// Representa o método que irá manipular o evento de recebimento de trade.
        /// </summary>
        /// <param name="tick"></param>
        public delegate void OnConnectHandler();

        /// <summary>Evento disparado quando de recebimento de yrade é executada.</summary>
        public static event OnConnectHandler OnConnect;

        #endregion

        #region Timers

        static System.Timers.Timer timerMonitorInternet = new System.Timers.Timer();
        static System.Timers.Timer timerMonitorReconnection = new System.Timers.Timer();

        #endregion

        #region Costrutor

        /// <summary>
        /// Construtor base do RealTimeDAO
        /// </summary>
        static RealTimeDAO()
        {
            
        }

        #endregion

        #region Eventos de recebimento de dados TCP

        /// <summary>
        /// Evento de recebimento de tick
        /// </summary>
        /// <param name="tick"></param>
        static void RealtimeTelnetServer_TickReceived(TickDTO tick)
        {
            if (TickReceived != null)
            {

                //adiciona na lista
                if (dictionaryTicks.ContainsKey(tick.Ativo))
                    dictionaryTicks[tick.Ativo] = tick;
                else
                    dictionaryTicks.Add(tick.Ativo, tick);

                //dispara evento para aqueles forms que estão aguardando o dado tick a tick
                //Assinando o evento de disparo de dados
                TickReceived(tick);


            }
        }

        /// <summary>
        /// Evento de recebimento de trade
        /// </summary>
        /// <param name="negocio"></param>
        static void RealtimeTelnetServer_NegocioReceived(NegocioDTO negocio)
        {
            if (TradeReceived != null)
                TradeReceived(negocio);
        }

        #endregion

        #region Metodos de Assinatura

        /// <summary>
        /// Metodo que faz a assinatura do ativo
        /// </summary>
        /// <param name="ativo"></param>
        public static void AssinaTick(string ativo)
        {
            if ((MarketDataDAO.IsBMF(ativo) && StaticData.UserHasBMFAccess) || (MarketDataDAO.IsBovespa(ativo) && StaticData.UserHasBovespaAccess))
            {
                //devo agurdar para estar conectado
                if (StaticData.StatusConexaoTelnetServer == StaticData.StatusConexao.Realtime)
                {
                    StaticData.RealtimeTelnet.SubscribeQuote(ativo);
                    if (!listTickSubscription.Contains(ativo))
                        listTickSubscription.Add(ativo);
                }
                else
                {
                    if (!listTickSubscription.Contains(ativo))
                        listTickSubscription.Add(ativo);
                }
            }

        }

        /// <summary>
        /// Metodo que faz a assinatura de trades
        /// </summary>
        /// <param name="ativo"></param>
        public static void AssinaTrade(string ativo)
        {
            if ( (MarketDataDAO.IsBMF(ativo) && StaticData.UserHasBMFAccess) || (MarketDataDAO.IsBovespa(ativo) && StaticData.UserHasBovespaAccess) )
            {
                if (StaticData.StatusConexaoTelnetServer == StaticData.StatusConexao.Realtime)
                {
                    StaticData.RealtimeTelnet.SubscribeTrade(ativo);
                    if (!listTradeSubscription.Contains(ativo))
                        listTradeSubscription.Add(ativo);
                }
                else
                {
                    if (!listTradeSubscription.Contains(ativo))
                        listTradeSubscription.Add(ativo);
                }
            }
            
        }

        #endregion

        #region Metodos

        public static void Disconnect()
        {
            StaticData.RealtimeTelnet.FechaConexao();
        }

        /// <summary>
        /// Metodo que vai fazer a conexao nos servidores de TCP Feeder
        /// </summary>
        public static void ConnectTelnetServer()
        {
            StaticData.RealtimeTelnet = new Realtime(false, "BMFBOVESPA", false);
            StaticData.RealtimeTelnet.IniciaConexao(StaticData.MARKETDATA_RT, 3002);
            StaticData.RealtimeTelnet.Login(StaticData.Login, StaticData.Senha);

            //eventos de recebimento de dados
            StaticData.RealtimeTelnet.TickReceived += new Realtime.TickHandler(RealtimeTelnetServer_TickReceived);
            StaticData.RealtimeTelnet.NegocioReceived += new Realtime.NegocioHandler(RealtimeTelnetServer_NegocioReceived);
            
            //Eventos para gerencias de manutenção da conexao
            StaticData.RealtimeTelnet.OnConnect += new Realtime.ConnectHandler(RealtimeTelnetServer_OnConnect);
            StaticData.RealtimeTelnet.OnError += new Realtime.ErrorHandler(RealtimeTelnetServer_OnError);
            StaticData.RealtimeTelnet.OnDisconnect += new Realtime.DisconnectHandler(RealtimeTelnetServer_OnDisconnect);
            
            //Timer de reconexao
            timerMonitorReconnection = new System.Timers.Timer();
            timerMonitorReconnection.Interval = 20000;
            timerMonitorReconnection.Elapsed += new System.Timers.ElapsedEventHandler(timerMonitorReconnection_Elapsed);

        }

        #endregion

        #region Eventos de Conexao e Desconexao

        /// <summary>
        /// Evento disparado apos o usuario se conectar com sucesso no servidor de dados em RT
        /// </summary>
        static void RealtimeTelnetServer_OnConnect()
        {
            try
            {
                StaticData.StatusConexaoTelnetServer = StaticData.StatusConexao.Realtime;
                timerMonitorReconnection.Start();

                //Percorre todas as assinaturas reassinado-as
                foreach (string ativo in listTickSubscription)
                {
                    AssinaTick(ativo);
                }
                foreach (string ativo in listTradeSubscription)
                {
                    AssinaTrade(ativo);
                }

                StaticData.LogEvent("Servidor Realtime conectado com sucesso.");

                if (OnConnect != null)
                    OnConnect();
            }
            catch (ObjectDisposedException dexc)
            {
                //Esta execeção é gerada quando o formulario é fechado enquanto recebe dados, não há necessidade de tratá-la
            }
            catch (InvalidOperationException iexc)
            {
                //Esta execeção é gerada quando o formulario é fechado enquanto recebe dados, não há necessidade de tratá-la
            }
        }

        /// <summary>
        /// Evento de erro disparado quando ocorre algumm problema no socket de comunicação
        /// </summary>
        /// <param name="exc"></param>
        static void RealtimeTelnetServer_OnError(Exception exc)
        {
            if (exc.GetType().ToString().Contains("SocketException"))
            {
                switch (((SocketException)exc).ErrorCode)
                {
                    case 10061:
                        StaticData.StatusConexaoTelnetServer = StaticData.StatusConexao.Offline;
                        break;
                    default:
                        //MessageBox.Show(exc.ToString());
                        break;
                }
            }

            StaticData.LogEvent("Servidor Realtime apresentou um erro:" + exc.ToString());
        }

        /// <summary>
        /// Evento disparado quando é identificado uma desconexao pelo Core.
        /// </summary>
        static void RealtimeTelnetServer_OnDisconnect()
        {
            try
            {
                if ((StaticData.UserHasBovespaAccess) || (StaticData.UserHasBMFAccess))
                {
                    if (StaticData.StatusConexaoTelnetServer == StaticData.StatusConexao.Realtime)
                        StaticData.StatusConexaoTelnetServer = StaticData.StatusConexao.DesconectadoPossivelQueda;
                }
                else
                    StaticData.StatusConexaoTelnetServer = StaticData.StatusConexao.NaoContratado;

                StaticData.LogEvent("Servidor Realtime foi desconectado");
            }
            catch (ObjectDisposedException dexc)
            {
                //Esta execeção é gerada quando o formulario é fechado enquanto recebe dados, não há necessidade de tratá-la
            }
            catch (InvalidOperationException iexc)
            {
                //Esta execeção é gerada quando o formulario é fechado enquanto recebe dados, não há necessidade de tratá-la
            }

        }

        /// <summary>
        /// Evento disparado para monitorar a reconexao
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void timerMonitorReconnection_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (StaticData.StatusConexaoTelnetServer == StaticData.StatusConexao.DesconectadoPossivelQueda)
            {
                if (StaticData.StatusInternet == StaticData.StatusConexao.Online)
                {
                    StaticData.StatusConexaoTelnetServer = StaticData.StatusConexao.Reconectando;
                    //
                    ConnectTelnetServer();
                }
            }

        }

        #endregion

        #region Metodos Auxiliares

        /// <summary>
        /// Metodo que retorna o ultimo tick
        /// </summary>
        /// <param name="ativo"></param>
        /// <returns></returns>
        public static TickDTO GetTick(string ativo)
        {
            if (dictionaryTicks.ContainsKey(ativo))
                return dictionaryTicks[ativo];
            else
                return null;
        }

        #endregion


    }
}
