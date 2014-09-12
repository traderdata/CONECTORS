using System.Windows.Forms;
using AmiBroker.Data;
using Traderdata.Client.ABConnector;

namespace Traderdata.Client.ABConnector.UI
{
    public partial class ConfigureForm : Form
    {
        private InfoSite infoSite;

        // constructor
        internal ConfigureForm(Configuration oldSettings, ref InfoSite infoSite)
        {
            this.infoSite = infoSite;

            InitializeComponent();

            //aqui deve popular com os settings de login e senha
            txtLogin.Text = oldSettings.Login;
            txtSenha.Text = oldSettings.Senha;
            txtProxyLogin.Text = oldSettings.ProxyLogin;
            txtProxyPassword.Text = oldSettings.ProxyPassword;
            txtHost.Text = oldSettings.Host;
        }

        // build config string from the dialog data
        internal Configuration GetNewSettings()
        {
            //devo retorna o login e senha
            Configuration defConfig = new Configuration();
            defConfig.Host = txtHost.Text;
            defConfig.Login = txtLogin.Text;
            defConfig.ProxyLogin = txtProxyLogin.Text;
            defConfig.ProxyPassword = txtProxyPassword.Text;
            defConfig.Senha = txtSenha.Text;
            if (rdbDCA.Checked)
                defConfig.DC = "A";
            else
                defConfig.DC = "B";
            defConfig.Aftermarket = chkAfterMarket.Checked;
            defConfig.DadoNominal = chkDadoNominal.Checked;

            return defConfig;
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            frmCadastro frmCadastro = new frmCadastro();
            frmCadastro.ShowDialog();
            txtLogin.Text = frmCadastro.txtEmail.Text;
            txtSenha.Text = frmCadastro.txtSenhaCadastro.Text;
        }

        private void button2_Click(object sender, System.EventArgs e)
        {
            Close();
        }

        private void btnConectar_Click(object sender, System.EventArgs e)
        {
            Close();
        }
    }
}
