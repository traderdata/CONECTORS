using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Traderdata.Client.eSginalConnector
{
    static class Program
    {

        public static string SECURITY_ENDPOINT = "https://wcf.traderdata.com.br/security-api/securityapi.svc";
        public static string MARKETDATA_ENDPOINT = "https://wcf.traderdata.com.br/md-api/mdapi.svc";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            StaticData.RealtimeTelnet = new Server.API.Realtime(false, "BMFBovespa", false);

            //Criando form de loign
            frmLoginCadastro frmLogin = new frmLoginCadastro("Traderdata");

            //fazendo login automatico
            frmLogin.ShowDialog();
            if (!frmLogin.StatusLogin)
                Application.Exit();
            else
                Application.Run(new frmMainAgent());
        }
    }
}
