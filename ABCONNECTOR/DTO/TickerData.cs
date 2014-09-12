using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmiBroker.Data;
using Traderdata.Server.Core.DTO;

namespace Traderdata.Client.ABConnector.DTO
{
    public class TickerData
    {
        /// <summary>
        /// Dicionario que armazena as cotações
        /// </summary>
        public QuotationList dictionaryQuotes = new QuotationList(Periodicity.OneMinute);

        /// <summary>
        /// Nome do ticker
        /// </summary>
        public string ticker = "";

        /// <summary>
        /// Status do ticker que inicia como vazio
        /// </summary>
        public Status statusTicker = Status.AppendRequested;

        /// <summary>
        /// Varialve que armazna o ultimo valor
        /// </summary>
        public double lastValue = 0;

        /// <summary>
        /// Varialve que armazena o ultimo tick para o ativo
        /// </summary>
        public TickDTO tickInfo = new TickDTO();
    }

    public enum Status { AppendRequested = 1, BackFillRequested = 2, RealtimeReadyToReceiveStreamingUpdates = 3, PedingRefreshUpdate = 4   }
}
