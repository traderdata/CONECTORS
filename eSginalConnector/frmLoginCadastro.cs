using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Mail;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using Traderdata.Server.API;
using Traderdata.Client.eSginalConnector.Util;
using Traderdata.Server.Core.DTO;

namespace Traderdata.Client.eSginalConnector
{
    public partial class frmLoginCadastro : Form
    {
        #region Variavel

        /// <summary>
        /// Variavel de login
        /// </summary>
        public bool StatusLogin = false;
        
        #endregion

        #region Construtor

        /// <summary>
        /// Construtor padrão
        /// </summary>
        public frmLoginCadastro(string Title)
        {
            InitializeComponent();

            //recuperando a versao do aplicativo
            lblVersion.Text = "Versão " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
            this.Text = Title;

            LembrarLoginSenha();
        }

        #endregion

        #region Metodos

        /// <summary>
        /// Metodo responsavel por carregar o login e senha nos campos corretos
        /// </summary>
        public string RestaurarDadosProxyHost()
        {
            try
            {
                //retorna o valor do login
                return GeneralUtil.RetornaValorRegistro("Host", "Host");

            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Metodo responsavel por carregar o login e senha nos campos corretos
        /// </summary>
        public string RestaurarDadosProxyLogin()
        {
            try
            {
                //retorna o valor do login
                return GeneralUtil.RetornaValorRegistro("ProxyLogin", "ProxyLogin");

            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Metodo responsavel por carregar o login e senha nos campos corretos
        /// </summary>
        public string RestaurarDadosProxyPassword()
        {
            try
            {
                //retorna o valor do login
                return GeneralUtil.RetornaValorRegistro("ProxyPassword", "ProxyPassword");

            }
            catch
            {
                return "";
            }
        }


        /// <summary>
        /// Metodo responsavel por salvar o login e senha do usuário no registro do windows
        /// </summary>
        public void AtualizaLoginSenha()
        {
            try
            {                
                if (chkSalvarLoginESenha.Checked == true)
                {

                    //Salvando Login
                    GeneralUtil.CriaArquivoNoRegistro(txtLogin.Text, "Login", "Login");

                    //Salvando Senha
                    GeneralUtil.CriaArquivoNoRegistro(txtSenha.Text, "Senha", "Login");
                    
                }
                
            }
            catch (Exception exc)
            {
                if (exc is UnauthorizedAccessException)
                {
                    MessageBox.Show("As configurações de segurança do UAC impediram o salvamento de seus dados.\nPara maiores informações visite: http://suporte.traderdata.com.br");                    
                }
                else
                {
                    MessageBox.Show("Falha ao salvar seus dados no registro. Detalhes do erro: " + exc.Message.ToString());
                }
            }
        }

        /// <summary>
        /// Metodo responsavel por carregar o login e senha nos campos corretos
        /// </summary>
        public void LembrarLoginSenha()
        {
            try
            {
                if (ConfigurationSettings.AppSettings["USER"] == null)
                {
                    //retorna o valor do login
                    txtLogin.Text = GeneralUtil.RetornaValorRegistro("Login", "Login");

                    //retorna o valro da senha
                    txtSenha.Text = GeneralUtil.RetornaValorRegistro("Senha", "Login");
                }
                else
                {
                    txtLogin.Text = ConfigurationSettings.AppSettings["USER"];
                    txtSenha.Text = ConfigurationSettings.AppSettings["PWD"];
                }
            }
            catch
            {
            }
        }
              
        /// <summary>
        /// Metodo que efetua o login
        /// </summary>
        private bool EfetuaLoginTraderdata(bool integrado)
        {            
            //checando se o usuario é nulo
            if (!Security.Login("", Program.SECURITY_ENDPOINT, txtLogin.Text.Trim(), txtSenha.Text.Trim(), "ESIGNAL"))
            {
                if (!integrado)
                {
                    txtLogin.Focus();
                    MessageBox.Show("Login e senha inválidos. Por favor tente novamente.");
                    return false;
                }
                else
                    return false;
            }

            //salvando no registro
            if (ConfigurationSettings.AppSettings["USER"] == null)
                AtualizaLoginSenha();

            //criando objeto local
            StaticData.Login = Security.User.Login;
            StaticData.Senha = txtSenha.Text.Trim();
            StaticData.UserHasBMFAccess = Security.User.HasBMFRT;
            StaticData.UserHasBovespaAccess = Security.User.HasBovespaRT;
            
            //setando o login como true e fechando o form            
            this.StatusLogin = true;
            this.Hide();

            //Inicializando Marketdata
            MarketData.InitializeAPI("", "https://wcf.traderdata.com.br/md-api/mdapi.svc", Security.User.Guid);

            return true;            
        }

        
        /// <summary>
        /// Metodo auxoliar que veriifca o preenchimento dos campos de cadastro
        /// </summary>
        /// <returns></returns>
        private string ValidarCadastro()
        {
            StringBuilder mensagem = new StringBuilder();
            if (!checkBox1.Checked)
                mensagem.AppendLine("- O eSignal Connector somente pode ser utilizado por Investidores Individuais (Não profissional).");

            if (txtNome.Text.Trim().Length <= 3)
                mensagem.AppendLine("- O campo  Nome Completo é obrigatório e deve conter mais de 3 caracteres.");

            if (!ValidaCPF())
                mensagem.AppendLine("- O campo  CPF é obrigatório e deve conter um CPF válido.");

            //checar se CPF ja existe
            if (Security.CPFCadastrado(txtCPF.Text, "td001", Program.SECURITY_ENDPOINT, ""))
                mensagem.AppendLine("- O CPF já consta em nossa base de dados.");

            if (!ValidaEmail())
                mensagem.AppendLine("- O campo  Email é obrigatório e deve conter um Email válido.");

            //checar se email ja existe
            if (Security.EmailCadastrado(txtEmail.Text, "td001", Program.SECURITY_ENDPOINT, ""))
                mensagem.AppendLine("- O email informado já consta em nossa base de dados.");


            if (txtSenhaCadastro.Text.Trim().Length <= 5)
                mensagem.AppendLine("- O campo Senha é obrigatório e deve conter mais que 5 caracteres.");
            if (txtSenhaCadastro.Text != txtConfirmaSenha.Text)
                mensagem.AppendLine("- O campo Senha deve ser o mesmo do campo Confirma Senha.");

            if (txtTelFixo.Text.Trim().Length <= 3)
                mensagem.AppendLine("- O campo  Tel. Fixo é obrigatório e deve conter mais de 3 caracteres.");

            if (txtCelular.Text.Trim().Length <= 3)
                mensagem.AppendLine("- O campo  Celular é obrigatório e deve conter mais de 3 caracteres.");

            
            return mensagem.ToString();
        }

        /// <summary>
        /// Metodo que valida se cpf é valido e inexistente na base
        /// </summary>
        /// <returns></returns>
        private bool ValidaCPF()
        {
            if ((txtCPF.Text.Trim().Length <= 10) || (!GeneralUtil.ValidaCPF(txtCPF.Text)))
                return false;
            else
            {
                return true;
            }

        }

        /// <summary>
        /// Metodo que valida se email é valido e inexistente na base
        /// </summary>
        /// <returns></returns>
        private bool ValidaEmail()
        {
            if (txtEmail.Text.Trim().Length <= 3)
                return false;
            else
            {
                try
                {
                    MailAddress m = new MailAddress(txtEmail.Text.Trim());
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            }

        }

        #endregion

       

        #region Eventos do Form
        
        /// <summary>
        /// Evento do clique no botão Fale Conosco
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label9_Click(object sender, EventArgs e)
        {
            Process.Start("http://messenger.providesupport.com/messenger/traderdata.html");
        }

        /// <summary>
        /// Evento executado ao se carregar o form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmLoginCadastro_Load(object sender, EventArgs e)
        {            
        
        }

        /// <summary>
        /// Evento disparado quando se clica rm Conectar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConectar_Click(object sender, EventArgs e)
        {
            Login(false);
        }

        public bool Login(bool integrado)
        {
            //setando proxy
            //StaticData.ProxyAddress = RestaurarDadosProxyHost();
            //StaticData.ProxyLogin = RestaurarDadosProxyLogin();
            //StaticData.ProxyPassword = RestaurarDadosProxyPassword();

            return EfetuaLoginTraderdata(integrado);                
            
        }

        /// <summary>
        /// Evento disparado quando se pressiona entre no campo login
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtLogin_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                EfetuaLoginTraderdata(false);
        }

        /// <summary>
        /// Evento disparado quando se pressiona enter no campo senha
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtSenha_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                EfetuaLoginTraderdata(false);
        }

        /// <summary>
        /// Evento disparado ao se clicar no btao cadastrar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCadastrar_Click(object sender, EventArgs e)
        {
            //StaticData.ProxyAddress = RestaurarDadosProxyHost();
            //StaticData.ProxyLogin= RestaurarDadosProxyLogin();
            //StaticData.ProxyPassword= RestaurarDadosProxyPassword();

            string mensagens = ValidarCadastro();

            if (mensagens.Length == 0)
            {                
                if (Security.InsertUser(txtNome.Text, txtCPF.Text, txtEmail.Text, txtEmail.Text, txtSenhaCadastro.Text, "", "", "ESIGNAL", "td001", Program.SECURITY_ENDPOINT, ""))
                {
                    MessageBox.Show("Usuário cadastrado com sucesso.");

                    //efetuando o login
                    txtLogin.Text = Security.User.Login;
                    txtSenha.Text = Security.User.Senha;
                    EfetuaLoginTraderdata(false);
                }

                //efetuando o login com o usuario e senha, caso nao tenha dado nada errado
            }
            else
            {
                MessageBox.Show(mensagens);
                return;
            }
        }

        #endregion

        private void button1_Click(object sender, EventArgs e)
        {
            frmProxy frmProxy = new frmProxy();
            frmProxy.ShowDialog();
        }


    }
}
