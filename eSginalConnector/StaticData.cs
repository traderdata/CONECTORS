using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Traderdata.Server.API;

namespace Traderdata.Client.eSginalConnector
{
    public static class StaticData
    {
        /// <summary>
        /// Login de acesso ao sistema
        /// </summary>
        public static string Login = "";

        /// <summary>
        /// Senha de acesso ao sistema
        /// </summary>
        public static string Senha = "";

        /// <summary>
        /// Flag que indica se cliente tem acesso a dados de BMF
        /// </summary>
        public static bool UserHasBMFAccess = false;

        /// <summary>
        /// Flag que indica se cliente tem acesso a dados de Bovesopa
        /// </summary>
        public static bool UserHasBovespaAccess = false;

        /// <summary>
        /// Lista de indices Bovespa
        /// </summary>
        public static List<string> IndicesBVSP = new List<string>();

        /// <summary>
        /// Conexao com o canal de TCP em realtime
        /// </summary>
        public static Realtime RealtimeTelnet;

        /// <summary>
        /// Enum com o status da conexao
        /// </summary>
        public enum StatusConexao { Realtime, PossivelAtraso, Atrasado, Offline, Reconectando, Conectando, KickPeloServer, DesconectadoPossivelQueda, Online, NaoContratado }

        /// <summary>
        /// Status da internetr
        /// </summary>
        public static StatusConexao StatusInternet = StatusConexao.Online;

        /// <summary>
        /// Status da conexao BVSP
        /// </summary>
        public static StatusConexao StatusConexaoTelnetServer = StatusConexao.Offline;

        #region Eventos de Log

        /// <summary>
        /// Representa o método que irá manipular o evento de recebimento de log.
        /// </summary>
        /// <param name="tick"></param>
        public delegate void LogHandler(string msg);

        /// <summary>Evento disparado quando a ação de StartTickSubscription é executada.</summary>
        public static event LogHandler LogReceived;

        public static void LogEvent(string msg)
        {
            if (LogReceived != null)
            {
                LogReceived("[" + DateTime.UtcNow.ToString() + "] - " + msg);
            }
        }

        #endregion

    }
}
