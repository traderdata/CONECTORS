using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Traderdata.Client.ABConnector
{
    [XmlRoot(Namespace = "Traderdata.Amibroker.DataConnector", IsNullable = false)]
    public class Configuration
    {
        public string Login;
        public string Senha;
        public string Host;
        public string ProxyLogin;
        public string ProxyPassword;
        public string DC;
        public bool Aftermarket { get; set; }
        public bool DadoNominal { get; set; }
                
        public static string GetConfigString(Configuration configuration)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Configuration));

            Stream stream = new MemoryStream();
            serializer.Serialize(XmlWriter.Create(stream), configuration);

            stream.Seek(0, SeekOrigin.Begin);
            StreamReader streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }

        public static Configuration GetConfigObject(string config)
        {
            // if no config string, set default values
            if (string.IsNullOrEmpty(config) || config.Trim().Length == 0)
                return GetDefaultConfigObject();

            XmlSerializer serializer = new XmlSerializer(typeof(Configuration));
            Stream stream = new MemoryStream(ASCIIEncoding.Default.GetBytes(config));

            try
            {
                return (Configuration)serializer.Deserialize(stream);
            }
            catch (Exception)
            {
                return GetDefaultConfigObject();
            }
        }

        public static Configuration GetDefaultConfigObject()
        {
            Configuration defConfig = new Configuration();
            defConfig.Host = "";
            defConfig.Login = "";
            defConfig.ProxyLogin = "";
            defConfig.ProxyPassword = "";            
            defConfig.Senha = "";
            defConfig.DC = "";
            return defConfig;
        }
    }
}
