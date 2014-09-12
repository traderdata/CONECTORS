using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.IO;
using Traderdata.Server.API;
using Traderdata.Server.Core.DTO;


namespace Traderdata.Client.eSginalConnector
{
    public static class MarketDataDAO
    {
        #region Variaveis

        #endregion

        #region Construtor

        static MarketDataDAO()
        {
            
        }

        #endregion

        #region Metodos

        /// <summary>
        /// Metodo que retorna os trades de um ativo de acorod com o numero maximo de barras solicitado
        /// </summary>
        /// <param name="ativo"></param>
        /// <param name="numeroMaximoBarras"></param>
        /// <returns></returns>
        public static List<NegocioDTO> GetTrades(string ativo, int numeroMaximoBarras)
        {   
            try
            {

                List<NegocioDTO> listaTrades = new List<NegocioDTO>();

                //checando se é nacional
                if (IsBMF(ativo) && StaticData.UserHasBMFAccess)
                {
                    return MarketData.GetTrades(ativo, false, numeroMaximoBarras);
                }
                else if (IsBovespa(ativo) && StaticData.UserHasBovespaAccess)
                {
                    return MarketData.GetTrades(ativo, true, numeroMaximoBarras);
                }

                
                return new List<NegocioDTO>();

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Metodo que retorna os trades de um ativo de acorod com o numero maximo de barras solicitado
        /// </summary>
        /// <param name="ativo"></param>
        /// <param name="numeroMaximoBarras"></param>
        /// <returns></returns>
        public static List<NegocioDTO> GetTradesPorData(string ativo, DateTime data)
        {
            try
            {
                List<NegocioDTO> listaTrades = new List<NegocioDTO>();

                //checando se é nacional
                if (IsBMF(ativo) && StaticData.UserHasBMFAccess)
                {
                    return MarketData.GetTrades(data.Date, data.Date.AddDays(1).AddSeconds(-1), ativo, true);
                }
                else if (IsBovespa(ativo) && StaticData.UserHasBovespaAccess)
                {
                    return MarketData.GetTrades(data.Date, data.Date.AddDays(1).AddSeconds(-1), ativo, true);
                }

                return new List<NegocioDTO>();

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Metodo que retorna o objeto ativo de acordo com o codigo passado
        /// </summary>
        /// <param name="ativo"></param>
        /// <returns></returns>
        public static AtivoDTO GetAtivo(string ativo)
        {
            return MarketData.GetSymbolByName(ativo);
        }

         //<summary>
         //Metodo que faz a inicialiação do canal de market data
         //</summary>
        public static void InitializeMarketDataChannel()
        {   
            //populando uma lista de indices Bovespa
            StaticData.IndicesBVSP = MarketData.GetIndexes();
        }

        /// <summary>
        /// Metodo que verifica se o ativo em questão é um indice
        /// </summary>
        /// <param name="indice"></param>
        /// <returns></returns>
        public static bool IsIndiceBovespa(string indice)
        {
            if (StaticData.IndicesBVSP.Contains(indice))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Metodo que verifica se um ativo é da Bovespa
        /// </summary>
        /// <param name="ativo"></param>
        /// <returns></returns>
        public static bool IsBovespa(string ativo)
        {
            AtivoDTO ativoDTO = MarketData.GetSymbolByName(ativo);
            if (ativoDTO != null && ativoDTO.Bolsa == 1)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Metodo que verifica se um ativo é da BMF
        /// </summary>
        /// <param name="ativo"></param>
        /// <returns></returns>
        public static bool IsBMF(string ativo)
        {
            AtivoDTO ativoDTO = MarketData.GetSymbolByName(ativo);
            if (ativoDTO != null && ativoDTO.Bolsa == 2)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Metodo que retorna as cotações diarias
        /// </summary>
        /// <param name="ativo"></param>
        public static List<CotacaoDTO> GetCotacaoDiaria(string ativo, DateTime dataLimite)
        {
            try
            {                
                //checando se é nacional
                if (IsBMF(ativo) && StaticData.UserHasBMFAccess) 
                {
                    return MarketData.GetDailyHistory(ativo, Int32.MaxValue);
                }
                else if (IsBovespa(ativo) && StaticData.UserHasBovespaAccess)
                {
                    return MarketData.GetDailyHistory(ativo, Int32.MaxValue);
                }

                return new List<CotacaoDTO>();
            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Metodo que retorna as cotações diarias
        /// </summary>
        /// <param name="ativo"></param>
        public static List<CotacaoDTO> GetCotacaoIntraday(string ativo, DateTime dtFrom, DateTime dataLimite)
        {            
            try
            {
                //checando se é nacional
                if (IsBMF(ativo) && StaticData.UserHasBMFAccess)
                {
                    return MarketData.GetIntradayHistory(ativo,  dtFrom, 1, false);
                }
                else if (IsBovespa(ativo) && StaticData.UserHasBovespaAccess)
                {
                    return MarketData.GetIntradayHistory(ativo,  dtFrom, 1, false);
                }

                return new List<CotacaoDTO>();
            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        #endregion

    }
        

}


