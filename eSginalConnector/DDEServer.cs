using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NDde.Server;
using Traderdata.Server.API;
using Traderdata.Server.Core.DTO;
using Traderdata.Client.eSginalConnector.DAO;

namespace Traderdata.Client.eSginalConnector
{
    public class DDEServer : DdeServer
    {
        #region Variaveis

        /// <summary>
        /// Dicionario contendo ultimo tick de cada ativo
        /// </summary>
        private Dictionary<string, TickDTO> dictionaryTick = new Dictionary<string, TickDTO>();

        public enum CamposDDeEnum
        {
            ULTIMO, VARIACAO, MIN, MAX, ABERTURA, VOLUME, MEDIA, DATA, MCOMPRA, MVENDA, MERCADO, NEGOCIOS,
            QULTIMO, QCOMPRA, QVENDA, QTOTAL, ULTFECH, HORA, INVALIDO, TICK
        };

        private System.Timers.Timer timerAdvise = new System.Timers.Timer();

        #endregion

        #region Construtor

        /// <summary>
        /// Constutor Padrao
        /// </summary>
        /// <param name="service"></param>
        public DDEServer()
            : base("TDS")
        {
            
            
            StaticData.RealtimeTelnet.TickReceived += new Realtime.TickHandler(RealtimeCore_TickReceived);
            
            //registrando o DDE Server
            Register();

            //iniciando timer
            timerAdvise.Interval = 500;
            timerAdvise.Elapsed += new System.Timers.ElapsedEventHandler(timerAdvise_Elapsed);
            timerAdvise.Start();
        }


        void timerAdvise_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Advise("*", "*");
        }

        

        #endregion

        #region Metodos de Override


        /// <summary>
        /// Metodo usado para registrar o DDE Server
        /// </summary>
        public override void Register()
        {
            base.Register();
        }

        /// <summary>
        /// Metodo usado para desregistrar o DDE Server
        /// </summary>
        public override void Unregister()
        {
            base.Unregister();
        }

        /// <summary>
        /// Metodo disparado quando se deixa de solicitar o ativo
        /// </summary>
        /// <param name="conversation"></param>
        /// <param name="item"></param>
        protected override void OnStopAdvise(DdeConversation conversation, string item)
        {
            //TODO:
            //nesse caso devemos cuidar da desassinatura
        }

        /// <summary>
        /// Metodo disparado pelo DDEClient quando solicita algum ativo
        /// </summary>
        /// <param name="conversation"></param>
        /// <param name="item"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        protected override bool OnStartAdvise(DdeConversation conversation, string item, int format)
        {
            //assinando cotação de ativo
            RealTimeDAO.AssinaTick(conversation.Topic.ToString().ToUpper());

            // Initiate the advisory loop only if the format is CF_TEXT.
            return format == 1;
        }

        /// <summary>
        /// Metodo disparado na hora de fazer o advise
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="item"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        protected override byte[] OnAdvise(string topic, string item, int format)
        {
            try
            {
                // Send data to the client only if the format is CF_TEXT.
                if (format == 1)
                {
                    //Verifica qual o papel
                    string ativo = topic.ToUpper().Trim();

                    TickDTO tickTemp = null;

                    if (!dictionaryTick.TryGetValue(ativo, out tickTemp))
                        return System.Text.Encoding.ASCII.GetBytes("");

                    
                    switch (item.ToUpper())
                    {
                        case "ULTIMO":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Ultimo.ToString());
                        case "VARIACAO":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Variacao.ToString());
                        case "MIN":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Minimo.ToString());
                        case "MAX":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Maximo.ToString());
                        case "ABERTURA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Abertura.ToString());
                        case "VOLUME":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Volume.ToString());
                        case "MEDIA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Media.ToString());
                        case "MCOMPRA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.MelhorOfertaCompra.ToString());
                        case "MVENDA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.MelhorOfertaVenda.ToString());
                        case "MERCADO":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Bolsa.ToString());
                        case "NEGOCIOS":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.NumeroNegocio.ToString());
                        case "QULTIMO":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.QuantidadeUltimoNegocio.ToString());
                        case "QCOMPRA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.QuantidadeMelhorOfertaCompra.ToString());
                        case "QVENDA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.QuantidadeMelhorOfertaVenda.ToString());
                        case "QTOTAL":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Quantidade.ToString());
                        case "ULTFECH":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.FechamentoAnterior.ToString());
                        case "HORA":
                            return System.Text.Encoding.ASCII.GetBytes(tickTemp.Data.ToString("HH:mm:ss"));

                        default:
                            return System.Text.Encoding.ASCII.GetBytes("");
                    }
                }
                else
                    throw new Exception("Formato inválido");
                

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }
        #endregion

        #region Eventos

        /// <summary>
        /// Evento de recebimento de ticks
        /// </summary>
        /// <param name="tick"></param>
        void RealtimeCore_TickReceived(TickDTO tick)
        {
            //atualizando o dicionario de ticks
            if (dictionaryTick.ContainsKey(tick.Ativo))
                dictionaryTick[tick.Ativo] = tick;
            else
                dictionaryTick.Add(tick.Ativo, tick);            
        }



        #endregion
    }
}
