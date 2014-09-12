using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AmiBroker;
using AmiBroker.Data;
using AmiBroker.PlugIn;
using System.Threading;
using System.Collections.Generic;
using Traderdata.Client.ABConnector.Util;
using Traderdata.Client.ABConnector.DTO;
using Traderdata.Client.ABConnector.UI;
using Traderdata.Client.ABConnector.DAO;
using System.Configuration;

namespace Traderdata.Client.ABConnector
{
    [ABDataSource("Traderdata ABConnector 2.2")]
    public class DataSource : DataSourceBase
    {
        #region Private variables

        private Database database;
        private Configuration config;
        private string databasePath;
        private string lastLongMessage;
        private int lastLongMessageTime;
        private bool allowMixedEodIntra;            // db have EOD and intraday data
        private int numBars;                        // max number of bars in the db
        private int timeBase;
                
        /// <summary>
        /// Seta o ticker corrente
        /// </summary>
        private string currentTicker = null;

        #region Context menu and form variables

        private ToolStripMenuItem mReconnect;
        private ToolStripMenuItem mForceBackfill;
        private ToolStripMenuItem mForceBackfillAll;        
        private ToolStripMenuItem mHelp;
        private ToolStripMenuItem mDisconnect;
        private ContextMenuStrip mContextMenu;

        #endregion

        #endregion

        #region Construtor

        public DataSource(string config)
            : base(config)
        {
            #region Context menu

            //Item de menu para reconexão
            mReconnect = new ToolStripMenuItem("Connect", null, new EventHandler(mReconnect_Click));
            //Item de menu para desconexão
            mDisconnect = new ToolStripMenuItem("Disconnect", null, new EventHandler(mDisconnect_Click));
            
            mForceBackfill = new ToolStripMenuItem("Refresh", null, new EventHandler(mForceBackfill_Click));
            mForceBackfillAll = new ToolStripMenuItem("Refresh All", null, new EventHandler(mForceBackfillAll_Click));            
            mHelp = new ToolStripMenuItem("Help", null, new EventHandler(mHelp_Click));

            ToolStripSeparator mSeparator = new ToolStripSeparator();
            ToolStripSeparator mSeparator2 = new ToolStripSeparator();
            ToolStripSeparator mSeparator3 = new ToolStripSeparator();
            ToolStripSeparator mSeparator4 = new ToolStripSeparator();
            ToolStripSeparator mSeparator5 = new ToolStripSeparator();
            ToolStripSeparator mSeparator6 = new ToolStripSeparator();

            mContextMenu = new ContextMenuStrip();
            mContextMenu.Items.AddRange(new ToolStripItem[] { mReconnect, mDisconnect, mSeparator2,
                mForceBackfill, mForceBackfillAll,mSeparator3, mHelp });
            
            SetContextMenuState();

            #endregion
        }

        #endregion

        #region AmiBroker's API calls

        /// <summary>
        /// Metodo que retornas as configurações ja salvas
        /// </summary>
        /// <param name="oldSettings"></param>
        /// <param name="infoSite"></param>
        /// <returns></returns>
        public static new string Configure(string oldSettings, ref InfoSite infoSite)
        {   
            Configuration configuration = Configuration.GetConfigObject(oldSettings);
            ConfigureForm frm = new ConfigureForm(configuration, ref infoSite);
            
            if (frm.ShowDialog() == DialogResult.OK)
                return Configuration.GetConfigString(frm.GetNewSettings());
            else
                return oldSettings;

        }

        /// <summary>
        /// Metodo que retorna as cotações historicas
        /// </summary>
        /// <param name="ticker"></param>
        /// <param name="quotes"></param>
        public override void GetQuotesEx(string ticker, ref QuotationArray quotes)
        {
            database.GetQuotesEx(ticker, ref quotes);
        }

        /// <summary>
        /// Metodo que atualiza a RealTimeQuotes
        /// </summary>
        /// <param name="ticker"></param>
        public override void GetRecentInfo(string ticker)
        {
            database.UpdateRecentInfo(ticker);
        }

        /// <summary>
        /// Metodo que retorna dados extras, pode ser usado para fazer comunicação entre o nosso conector e o AMIBroker
        /// </summary>
        /// <param name="ticker"></param>
        /// <param name="name"></param>
        /// <param name="periodicity"></param>
        /// <param name="arraySize"></param>
        /// <returns></returns>
        public override AmiVar GetExtraData(string ticker, string name, Periodicity periodicity, int arraySize)
        {            
            return new AmiVar();
        }
              
        /// <summary>
        /// Metodo responsável por informar o status do plugin
        /// </summary>
        /// <returns></returns>
        public override PluginStatus GetStatus()
        {   
            PluginStatus status = new PluginStatus();

            if (database.IsConnected)
            {
                status.Status = StatusCode.OK;
                status.Color = System.Drawing.Color.LimeGreen;
                status.ShortMessage = "AB CONNECTOR 2.0 - OK";
            }
            else if (database.IsLoading)
            {
                status.Status = StatusCode.Warning;
                status.Color = System.Drawing.Color.Yellow;
                status.ShortMessage = "AB CONNECTOR 2.0 - LOADING";
            }
            else if (database.IsDisconnected)
            {
                status.Status = StatusCode.Warning;
                status.Color = System.Drawing.Color.Red;
                status.ShortMessage = "AB CONNECTOR 2.0 - ERROR";
            }

            status.LongMessage = LogAndMessage.GetMessages();

            // if there is no message, we show short message
            if (string.IsNullOrEmpty(status.LongMessage))
            {
                status.LongMessage = status.ShortMessage;
                // save as the last shown message to avoid status popup
                lastLongMessage = status.ShortMessage;
            }

            // if new message we use a new lastLongMessageTime value to cause status popup
            if (lastLongMessage != status.LongMessage)
            {
                lastLongMessage = status.LongMessage;
                lastLongMessageTime = (int)DateTime.Now.TimeOfDay.TotalMilliseconds;
            }

            // set status and "timestamp"
            status.Status = (StatusCode)((int)status.Status + lastLongMessageTime);

            SetContextMenuState();

            return status;
        }

        /// <summary>
        /// Timebases permitidos ao plugin
        /// </summary>
        /// <param name="timeBase"></param>
        /// <returns></returns>
        public override bool SetTimeBase(Periodicity timeBase)
        {
            return timeBase == Periodicity.OneMinute || timeBase == Periodicity.Tick || timeBase == Periodicity.EndOfDay;
        }

        /// <summary>
        /// Limite de symbolos que podem ser carregados simultaneamente
        /// </summary>
        /// <returns></returns>
        public override int GetSymbolLimit()
        {
            // limit the symbols to handle concurrently
            return 100;
        }
        
        /// <summary>
        /// Eventos disparados por diversos eventos dentro do AB
        /// </summary>
        /// <param name="notifyData"></param>
        /// <returns></returns>
        public override bool Notify(ref PluginNotification notifyData)
        {
            bool result = true;

            switch (notifyData.Reason)
            {
                case Reason.DatabaseLoaded:

                    // if database is loaded
                    if (database != null)
                    {
                        // disconnect from data provider and reset all data
                        database.Disconnect();
                    }

                    // start logging the opening of the database
                    LogAndMessage.Log(MessageType.Info, "Database: " + notifyData.DatabasePath);

                    databasePath = notifyData.DatabasePath;

                    allowMixedEodIntra = notifyData.Workspace.AllowMixedEODIntra != 0;
                    LogAndMessage.Log(MessageType.Info, "Mixed EOD/Intra: " + allowMixedEodIntra);

                    timeBase = notifyData.Workspace.TimeBase;

                    numBars = notifyData.Workspace.NumBars;
                    LogAndMessage.Log(MessageType.Info, "Number of bars: " + numBars);

                    LogAndMessage.Log(MessageType.Info, "Database config: " + Settings);

                    // create the config object
                    config = Configuration.GetConfigObject(Settings);              
                    StaticData.Login = config.Login;
                    StaticData.Senha = config.Senha;
                    if ((config.DC == null) ||(config.DC == "A"))
                    {                        
                        StaticData.MARKETDATA_RT = "app-cma.traderdata.com.br";
                        StaticData.SECURITY_ENDPOINT = "https://app-cma.traderdata.com.br/security-api/securityapi.svc";
                        StaticData.MARKETDATA_ENDPOINT = "https://app-cma.traderdata.com.br/md-api/mdapi.svc";
                    }
                    else
                    {
                        StaticData.MARKETDATA_RT = "app-diveo.traderdata.com.br";
                        StaticData.SECURITY_ENDPOINT = "https://app-diveo.traderdata.com.br/security-api/securityapi.svc";
                        StaticData.MARKETDATA_ENDPOINT = "https://app-diveo.traderdata.com.br/md-api/mdapi.svc";
                    }


                    //StaticData.ProxyLogin = config.ProxyLogin;
                    //StaticData.ProxyPassword = config.ProxyPassword;
                    //StaticData.ProxyAddress = config.Host;            

                    // create new database object
                    database = new Database(config, notifyData.Workspace);

                    // connect database to data provider
                    database.Connect();

                    break;

                // user changed the db
                case Reason.DatabaseUnloaded:

                    // disconnect from data provider
                    if (database != null)
                        database.Disconnect();

                    // clean up
                    database = null;

                    break;

                // seams to be obsolete
                case Reason.SettingsChanged:

                    break;

                // user right clicks data plugin area in AB
                case Reason.RightMouseClick:

                    if (database != null)
                    {
                        if (notifyData.CurrentSI != null)
                        {
                            currentTicker = notifyData.CurrentSI.ShortName;
                        }

                        SetContextMenuState();

                        ShowContextMenu(mContextMenu);
                    }

                    break;

                default: result = false;

                    break;
            }
            return result;
        }

        #endregion

        #region Context menu

        private void mReconnect_Click(object sender, EventArgs e)
        {
            LogAndMessage.Log(MessageType.Info, "Manually reconnected.");
            database.Connect();
        }

        /// <summary>
        /// Evento de execução do backfill
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mForceBackfill_Click(object sender, EventArgs e)
        {
            Database.dictionaryTicker[currentTicker].statusTicker = Status.BackFillRequested;
            DataSourceBase.NotifyQuotesUpdate();
        }

        /// <summary>
        /// Evento de execução do backfill all
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mForceBackfillAll_Click(object sender, EventArgs e)
        {
            lock (Database.dictionaryTicker)
            {
                foreach (string obj in Database.dictionaryTicker.Keys)
                {
                    Database.dictionaryTicker[obj].statusTicker = Status.BackFillRequested;
                }
                DataSourceBase.NotifyQuotesUpdate();
            }        
        }

        /// <summary>
        /// Metodo disparado ao se clicar sobre Disconnect
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mDisconnect_Click(object sender, EventArgs e)
        {            
            
            LogAndMessage.Log(MessageType.Info, "Manually disconnected.");

            database.Disconnect();
        }
        
        /// <summary>
        /// Metodo disparado ao se clicar sobre Help
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mHelp_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://wiki.traderdata.com.br/home/traderdata-ab-connector");
        }

        /// <summary>
        /// Metodo usado para apresentar o Log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mOpenLogFile_Click(object sender, EventArgs e)
        {
            const string npp = @"C:\Program Files (x86)\Notepad++\notepad++.exe";

            ProcessStartInfo psi;

            try
            {
                // check if notepad++ is installed
                if (File.Exists(npp))
                    // start notepad++ to open the log file
                    psi = new ProcessStartInfo(npp);
                else
                    // start notepad to open the log file
                    psi = new ProcessStartInfo("notepad.exe");

                psi.WorkingDirectory = Path.GetDirectoryName(DataSourceBase.DotNetLogFile);
                psi.Arguments = DataSourceBase.DotNetLogFile;

                // start log file viewer
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not start notepad.exe to open instace log file:" + Environment.NewLine + ex);
            }
        }
        
        /// <summary>
        /// Metodo que seta o contexto
        /// </summary>
        private void SetContextMenuState()
        {            
            if (database == null)
            {
                mReconnect.Enabled = false;
                mDisconnect.Enabled = false;
            }
            else
            {
                mReconnect.Enabled = !database.IsConnected;
                mDisconnect.Enabled = database.IsConnected;
                mForceBackfill.Enabled = database.IsConnected;
                mForceBackfillAll.Enabled = database.IsConnected;                
            }
        }

        #endregion
    }
}
