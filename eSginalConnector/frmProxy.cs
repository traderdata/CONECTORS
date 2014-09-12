using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Traderdata.Client.eSginalConnector.Util;


namespace Traderdata.Client.eSginalConnector
{
    public partial class frmProxy : Form
    {
        public frmProxy()
        {
            InitializeComponent();

            //resgatando dados de proxy
            RestaurarDados();
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            Close();
        }

        #region Metodos

        /// <summary>
        /// Metodo responsavel por salvar o login e senha do usuário no registro do windows
        /// </summary>
        public void SalvarDados()
        {
            try
            {
                //Salvando Login
                GeneralUtil.CriaArquivoNoRegistro(txtHost.Text, "Host", "Host");
                GeneralUtil.CriaArquivoNoRegistro(txtLogin.Text, "ProxyLogin", "ProxyLogin");
                GeneralUtil.CriaArquivoNoRegistro(txtPassword.Text, "ProxyPassword", "ProxyPassword");
            }
            catch (Exception exc)
            {
                if (exc is UnauthorizedAccessException)
                {
                    MessageBox.Show("As configurações de segurança do UAC impediram o salvamento de seus dados.\nPara maiores informações visite: http://wiki.traderdata.com.br");
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
        public void RestaurarDados()
        {
            try
            {
                //retorna o valor do login
                txtHost.Text = GeneralUtil.RetornaValorRegistro("Host", "Host");
                txtLogin.Text = GeneralUtil.RetornaValorRegistro("ProxyLogin", "ProxyLogin");
                txtPassword.Text = GeneralUtil.RetornaValorRegistro("ProxyPassword", "ProxyPassword");
            }
            catch
            {
            }
        }

        #endregion

        #region Unsubscribe
        public void Unsubscribe()
        {
        }
        #endregion

        private void btnAplicar_Click(object sender, EventArgs e)
        {
            SalvarDados();
            Close();
        }
    }
}
