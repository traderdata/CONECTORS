using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Mail;
using Traderdata.Client.ABConnector.DAO;
using Traderdata.Client.ABConnector.Util;
using Traderdata.Client.ABConnector.DTO;
using Traderdata.Server.API;

namespace Traderdata.Client.ABConnector.UI
{
    public partial class frmCadastro : Form
    {
        public frmCadastro()
        {
            InitializeComponent();
        }

        #region Metodos

        /// <summary>
        /// Metodo auxoliar que veriifca o preenchimento dos campos de cadastro
        /// </summary>
        /// <returns></returns>
        private string ValidarCadastro()
        {
            StringBuilder mensagem = new StringBuilder();
            if (txtNome.Text.Trim().Length <= 3)
                mensagem.AppendLine("- O campo  Nome Completo é obrigatório e deve conter mais de 3 caracteres.");
            if (!ValidaCPF())
                mensagem.AppendLine("- O campo  CPF é obrigatório e deve conter um CPF válido.");

            //checar se CPF ja existe
            if (Security.CPFCadastrado(txtCPF.Text, StaticData.SECURITY_TOKEN, StaticData.SECURITY_ENDPOINT, ""))
                mensagem.AppendLine("- O CPF já consta em nossa base de dados.");

            if (!ValidaEmail())
                mensagem.AppendLine("- O campo  Email é obrigatório e deve conter um Email válido.");

            //checar se email ja existe
            if (Security.EmailCadastrado(txtEmail.Text, StaticData.SECURITY_TOKEN, StaticData.SECURITY_ENDPOINT, ""))
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

        private void btnCadastrar_Click(object sender, EventArgs e)
        {            
            string mensagens = ValidarCadastro();

            if (mensagens.Length == 0)
            {
                if (Security.InsertUser(txtNome.Text, txtCPF.Text, txtEmail.Text, txtEmail.Text, txtSenhaCadastro.Text, txtTelFixo.Text, txtCelular.Text, "ABCONNECTOR",  StaticData.SECURITY_TOKEN
                    , StaticData.SECURITY_ENDPOINT, ""))
                {
                    MessageBox.Show("Usuário cadastrado com sucesso.");
                    Close();
                }

            }
            else
            {
                MessageBox.Show(mensagens);
                return;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
