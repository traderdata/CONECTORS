using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Traderdata.Client.eSginalConnector.DAO;
using Traderdata.Server.Core.DTO;
using System.Diagnostics;

namespace Traderdata.Client.eSginalConnector
{
    public partial class frmMainAgent : Form
    {
        #region Variaveis

        /// <summary>
        /// Classe de conexao DDE
        /// </summary>
        private DDEServer ddeServer;

        /// <summary>
        /// Classe de emulção de eSignal
        /// </summary>
        private eSignalServer eSignalServer;

        #endregion

        #region Construtor

        public frmMainAgent()
        {
            InitializeComponent();

            
            //inicializando serviço conectado
            RealTimeDAO.ConnectTelnetServer();

            //iniciando DDEServer
            ddeServer = new DDEServer();

            //iniciando esignalServer
            eSignalServer = new eSignalServer();
            
        }

        #endregion

        private void sairToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void conectarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //inicializando serviço conectado
            RealTimeDAO.ConnectTelnetServer();
        }

        private void desconectarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RealTimeDAO.Disconnect();
        }

        private void frmMainAgent_Load(object sender, EventArgs e)
        {
            StaticData.LogReceived += new StaticData.LogHandler(StaticData_LogReceived);
        }

        void StaticData_LogReceived(string msg)
        {
            try
            {
                richTextBox1.Invoke(new Action(() => richTextBox1.AppendText('\n' + msg)));
            }
            catch (ObjectDisposedException dexc)
            {
                //Esta execeção é gerada quando o formulario é fechado enquanto recebe dados, não há necessidade de tratá-la
            }
            catch (InvalidOperationException iexc)
            {
                //Esta execeção é gerada quando o formulario é fechado enquanto recebe dados, não há necessidade de tratá-la
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
            
        }

        private void emailToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("mailto:suporte@traderdata.com.br");
        }

        private void chatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("http://messenger.providesupport.com/messenger/traderdata.html");
        }

        private void reconexaoAutomáticaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            conectarToolStripMenuItem.Enabled = !reconexaoAutomáticaToolStripMenuItem.Checked;
            desconectarToolStripMenuItem.Enabled = !reconexaoAutomáticaToolStripMenuItem.Checked;
        }
    }
}
