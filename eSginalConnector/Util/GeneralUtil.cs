using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Traderdata.Client.eSginalConnector.Util
{
    public static class GeneralUtil
    {
        #region Variaveis

        /// <summary>
        /// Handle 
        /// </summary>
        private static IntPtr _currentHandle;

        public enum TipoBoleta { Compra, Venda }

        public enum TipoStopStart { StopLoss, StopGain, Start }

        #endregion

        #region Metodos

        #region Image converter

        public static void SaveJpeg(this Image img, string filePath, long quality)
        {
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            img.Save(filePath, GetEncoder(ImageFormat.Jpeg), encoderParameters);
        }

        public static void SaveJpeg(this Image img, Stream stream, long quality)
        {
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            img.Save(stream, GetEncoder(ImageFormat.Jpeg), encoderParameters);
        }

        static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            return codecs.Single(codec => codec.FormatID == format.Guid);
        }

        #endregion

        /// <summary>
        /// Provider padrão para número. Utiliza 2 casas decimais e separador decimal ".".
        /// </summary>
        public static NumberFormatInfo NumberProvider
        {
            get
            {
                NumberFormatInfo numberProvider = new NumberFormatInfo();
                numberProvider.NumberDecimalDigits = 2;
                numberProvider.CurrencyDecimalDigits = 2;
                numberProvider.PercentDecimalDigits = 2;
                
                numberProvider.NumberDecimalSeparator = ".";
                numberProvider.NumberGroupSeparator = "";

                return numberProvider.Clone() as NumberFormatInfo;
            }
        }

        /// <summary>
        /// Metodo Retorna o ip local do cliente
        /// </summary>
        /// <returns></returns>
        public static string LocalIPAddress()
        {
            IPHostEntry host;
            string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                }
            }
            return localIP;
        }


        /// <summary>
        /// Metodo que faz a validação de CPF
        /// </summary>
        /// <param name="cpf"></param>
        /// <returns></returns>
        public static bool ValidaCPF(string cpf)
        {

            int[] multiplicador1 = new int[9] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            int[] multiplicador2 = new int[10] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

            string tempCpf;

            string digito;

            int soma;

            int resto;

            cpf = cpf.Trim();

            cpf = cpf.Replace(".", "").Replace("-", "");

            if (cpf.Length != 11)

                return false;

            tempCpf = cpf.Substring(0, 9);

            soma = 0;

            for (int i = 0; i < 9; i++)

                soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

            resto = soma % 11;

            if (resto < 2)

                resto = 0;

            else

                resto = 11 - resto;

            digito = resto.ToString();

            tempCpf = tempCpf + digito;

            soma = 0;

            for (int i = 0; i < 10; i++)

                soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

            resto = soma % 11;

            if (resto < 2)

                resto = 0;

            else

                resto = 11 - resto;

            digito = digito + resto.ToString();

            return cpf.EndsWith(digito);

        }

        #endregion

        #region Metodos de Registro

        #region RetornaValorRegistro()
        /// <summary>
        /// Retorna o valor guardado em um arquivo, contido em uma determinada chave (contida em "TDSystemInfo").
        /// </summary>
        /// <param name="nomeArquivo"></param>
        /// <param name="subChaves"></param>
        /// <returns></returns>
        public static string RetornaValorRegistro(string nomeArquivo, params string[] subChaves)
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("Software", true);
                string valor = "";

                rk = rk.OpenSubKey("TraderdataLite", true);

                for (int i = 0; i <= subChaves.Length - 1; i++)
                {
                    rk = rk.OpenSubKey(subChaves[i]);
                }

                //Recupera o valor no registro
                valor = rk.GetValue(nomeArquivo).ToString();

                //Fecha chave
                rk.Close();

                return valor;
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }
        #endregion RetornaValorRegistro()

        #region CriaArquivoNoRegistro()
        /// <summary>
        /// Cria o registro, pelo nome da Chave (subpasta de "TDSystemInfo")e pelo nome do arquivo que
        /// conterá o valor a ser gravado.
        /// </summary>
        /// <param name="valor"></param>
        /// <param name="nomeArquivo"></param>
        /// <param name="subChaves"></param>
        public static void CriaArquivoNoRegistro(string valor, string nomeArquivo, params string[] subChaves)
        {
            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("Software", true);

                // Cria um Subchave (Principal) e uma chave secundaria (dentro da principal)
                rk = rk.CreateSubKey("TraderdataLite");

                for (int i = 0; i <= subChaves.Length - 1; i++)
                {
                    rk = rk.CreateSubKey(subChaves[i]);
                }

                // Grava o login
                rk.SetValue(nomeArquivo, valor);

                // fecha a Chave de Restistro registro
                rk.Close();
            }
            catch(Exception exc)
            {

            }
        }
        #endregion CriaArquivoNoRegistro()

        #region AtualizaArquivoNoRegistro()
        /// <summary>
        /// Atualiza o registro, pelo nome da Chave (subpasta de "TDSystemInfo")e pelo nome do arquivo que
        /// conterá o valor a ser gravado.
        /// </summary>
        /// <param name="valor"></param>
        /// <param name="nomeArquivo"></param>
        /// <param name="subChaves"></param>
        public static  void AtualizaArquivoNoRegistro(string valor, string nomeArquivo, params string[] subChaves)
        {

            try
            {
                RegistryKey rk = Registry.LocalMachine.OpenSubKey("Software", true);

                //Abre a Chave do TDS
                rk = rk.CreateSubKey("TraderdataLite");

                //Abre as subChaves recebidas
                for (int i = 0; i <= subChaves.Length - 1; i++)
                {
                    rk = rk.CreateSubKey(subChaves[i]);
                }

                // Grava o login
                rk.SetValue(nomeArquivo, valor);

                // fecha a Chave de Restistro registro
                rk.Close();
            }
            catch
            {

            }
        }
        #endregion AtualizaArquivoNoRegistro()

        #region RegistroExistente()
        /// <summary>
        /// Verifica se o registro existe.
        /// </summary>
        /// <param name="nomeArquivo">Nome do arquivo no registro.</param>
        /// <param name="subChaves">Chaves/caminhos para o registro. Pode-se omitir a pasta raiz TDSystemInfo.</param>
        /// <returns></returns>
        public static bool RegistroExistente(string nomeArquivo, params string[] subChaves)
        {
            try
            {
                string caminho = "Software\\TraderdataLite\\";

                for (int i = 0; i <= subChaves.Length - 1; i++)
                {
                    if (subChaves.Length - 1 != i)
                        caminho += subChaves[i] + "\\";
                    else
                        caminho += subChaves[i];
                }

                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(caminho, true))
                {
                    //Recupera o valor no registro
                    string valor = rk.GetValue(nomeArquivo).ToString();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion RegistroExistente()

        #endregion

    }
}
