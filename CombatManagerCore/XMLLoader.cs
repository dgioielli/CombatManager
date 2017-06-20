/*
 *  XMLLoader.cs
 *
 *  Copyright (C) 2010-2012 Kyle Olson, kyle@kyleolson.com
 *
 *  This program is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU General Public License
 *  as published by the Free Software Foundation; either version 2
 *  of the License, or (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 * 
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 *
 */

﻿using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
#if ANDROID
using Android.Content;
#endif

namespace CombatManager
{
    /// <summary>
    /// Classe estática responsável por recuperar um conjunto de dados de um arquivo 
    ///  XML e carregar esse arquivo numa lista de Ts.
    /// </summary>
    /// <typeparam name="T">Tipo da variável de dados.</typeparam>
    public static class XmlListLoader<T>
    {
        /// <summary>
        /// Carrega lista do Arquivo
        /// </summary>
        /// <param name="filename">Nome do Arquivo XML</param>
        /// <param name="appData">Indica se o arquivo está no appdata</param>
        /// <returns>Lista de dados Carregada</returns>
        public static List<T> Load(string filename, bool appData = false)
        {
            return XmlLoader<List<T>>.Load(filename, appData);
        }

        /// <summary>
        /// Grava uma lista de dados em um arquivo
        /// </summary>
        /// <param name="list">Lista de dados</param>
        /// <param name="filename">Nome do arquivo</param>
        /// <param name="appData">Indica se o arquivo está no appdata</param>
        public static void Save(List<T> list, string filename, bool appData = false)
        {
            XmlLoader<List<T>>.Save(list, filename, appData);
        }
    }

    /// <summary>
    /// Classe estática responsável por recuperar um dado de um arquivo XML e carregar
    ///  esse dado numa variável do tipo T
    /// </summary>
    /// <typeparam name="T">Tido da variável dos dados</typeparam>
    public class XmlLoader<T>
    {
        #region Constantes

        /// <summary>
        /// Nome da pasta no Appdata onde os dados do usuário vão ficar armazenados.
        /// </summary>
        public const string AppDataSubDir = "Combat Manager";

        #endregion

        #region Variáveis Estáticas

        /// <summary>
        /// Ainda não sei para que essa variável serve
        /// </summary>
        private static Dictionary<string, string> xmlAttributeErrors;
        /// <summary>
        /// Variável que armazena o nome do último arquivo utilizado.
        /// </summary>
        private static string lastFile;

        /// <summary>
        /// Varável que armazena o diretório onde está a aplicação.
        /// </summary>
        private static string _AssemblyDir;
        /// <summary>
        /// Variável que armazena o diretório no Appdata onde estão os dados do usuário.
        /// </summary>
        private static string _AppDataDir;

        /// <summary>
        /// Varável que armazena um serializador para o tipo T
        /// </summary>
        private static XmlSerializer _Serializer = new XmlSerializer(typeof(T));

        #endregion

        #region Propriedades Estáticas

        /// <summary>
        /// Propriedade que guarda o diretório da aplicação.
        /// Na primeira vez que o sistema procura pelo diretório da aplicação ele grava
        ///  essa informação na memória, para acessos mais rápidos.
        /// </summary>
        public static string AssemblyDir
        {
            get
            {
                if (_AssemblyDir == null)
                {
                    System.Diagnostics.Debug.WriteLine("AssemblyDir");
                    _AssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);


#if MONO
                    int loc = _AssemblyDir.IndexOf("/.monotouch");
                    if (loc > 0) 
                    {
                        _AssemblyDir = _AssemblyDir.Substring(0, loc);
                    }

#endif
                }
                return _AssemblyDir;
            }
        }

        /// <summary>
        /// Propriedade que guarda o diretório no appdata com os dados do usuário.
        /// Na primeira vez que o sistema procura pelo diretório ele grava essa informação
        ///  na memória, para acesso mais rápido.
        /// </summary>
        public static string AppDataDir
        {
            get
            {
                if (_AppDataDir == null)
                {
                    System.Diagnostics.Debug.WriteLine("AppDataDir");
#if ANDROID
                    _AppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
#else
                    _AppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#endif
                    _AppDataDir = Path.Combine(_AppDataDir, AppDataSubDir);

                    if (!Directory.Exists(_AppDataDir))
                    {
                        Directory.CreateDirectory(_AppDataDir);
                    }

                }
                return _AppDataDir;
            }
        }

        #endregion

        #region Funções Estáticas

        /// <summary>
        /// Função responsável por carregar os dados de um arquivo XML em uma variável do tipo T
        /// </summary>
        /// <param name="filename">Nome do arquivo</param>
        /// <param name="appData">Indicação para dizer se o arquivo está no appdata</param>
        /// <returns>Dado Carregado</returns>
        public static T Load(string filename, bool appData = false)
        {
            T set = default(T);

            lastFile = filename;

#if MONO
			DateTime startTime = DateTime.Now;
			DebugLogger.WriteLine("Loading [" + filename + "]");
#endif

            try
            {
                // Open document
                XmlSerializer serializer = new XmlSerializer(typeof(T));

                serializer.UnknownNode += new XmlNodeEventHandler(serializer_UnknownNode);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(serializer_UnknownAttribute);

#if ANDROID
                string path = filename;
                Stream io;
                StreamReader fs;
                if (!appData)
                {
                    if (path.StartsWith("/"))
                    {
                        io = File.Open(path, FileMode.Open);
                        fs = new StreamReader(io);

                    }
                    else
                    {
                      
                        io = CoreContext.Context.Assets.Open(path);
                        fs = new StreamReader(io);
                    }
                    
                }
                else
                {
                    path = Path.Combine(AppDataDir, filename);
                    io = File.Open(path, FileMode.Open);
                    fs = new StreamReader(io);

                }
                using(io)
                {
                    using (fs)
                    {
                    
#else

                String file = SaveFileName(filename, appData);

                if (new FileInfo(file).Exists)
                {
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
#endif

                        xmlAttributeErrors = new Dictionary<string, string>();

                        set = (T)serializer.Deserialize(fs);

                        if (xmlAttributeErrors.Count > 0)
                        {
                            DebugLogger.WriteLine("XmlListLoader: " + lastFile);
                            foreach (string st in xmlAttributeErrors.Keys)
                            {
                                DebugLogger.WriteLine(st);
                            }
                        }

#if !ANDROID
                        fs.Close();
#endif  
                    }
                }
            }
            catch (Exception ex)
            {
                
                DebugLogger.WriteLine(ex.ToString());
                if (!appData)
                {
                    throw;
                }
            }

#if MONO
			DebugLogger.WriteLine("Finished [" + filename + "]  Time: " + 
				(DateTime.Now - startTime).TotalSeconds.ToString() + " secs");
#endif

            return set;
        }

        /// <summary>
        /// Função responsável por fazer a gravação dos dados em um arquivo.
        /// </summary>
        /// <param name="list">Dados que serão gravados</param>
        /// <param name="filename">Nome do arquivo onde será feita a gravação.</param>
        /// <param name="appData">Indica se o arquivo está no appdata</param>
        public static void Save(T list, string filename, bool appData = false)
        {
            String file = SaveFileName(filename, appData);
            FileInfo fi = new FileInfo(file);

#if MONO
            //DateTime startTime = DateTime.Now;
            //DebugLogger.WriteLine("Saving [" + fi.Name + "]");
#endif

            //lastFile = filename;

            TextWriter writer = new StreamWriter(filename);

            XmlTextWriter xmlWriter = new XmlTextWriter(writer);

            _Serializer.Serialize(xmlWriter, list);
            writer.Close();
#if MONO
            //DebugLogger.WriteLine("Finished [" + fi.Name + "]  Time: " + 
            //    (DateTime.Now - startTime).TotalSeconds.ToString() + " secs");
#endif
        }

        /// <summary>
        /// Função responsável por pegar o caminho completo de um arquivo.
        /// </summary>
        /// <param name="filename">Nome do arquivo</param>
        /// <param name="appData">Indica se o arquivo está no appdata</param>
        /// <returns>Caminho completo do arquivo.</returns>
        public static String SaveFileName(String filename, bool appData = false)
        {
            string path;
            if (appData)
                path = AppDataDir;
            else
                path = AssemblyDir;

            return Path.Combine(path, filename);
        }

        /// <summary>
        /// Função responsável por deletar as informações de um arquivo.
        /// Não sei se eu entendi a utilização dessa função.
        /// </summary>
        /// <param name="filename">Nome do arquivo</param>
        /// <param name="appData">Indica se o arquivo está no appdata.</param>
        public static void Delete(String filename, bool appData = false)
        {
            String file = SaveFileName(filename, appData);

            FileInfo info = new FileInfo(file);
            if (info.Exists)
                info.Delete();
        }

        #endregion

        #region Eventos nessa classe

        /// <summary>
        /// Evento que é disparado quando o Serializador do XML encontra um Atributo Desconhecido.
        /// Se o esse erro está aparecendo pela primeira vez ele grava na lista de erros.
        /// </summary>
        private static void serializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            if (xmlAttributeErrors != null)
                if (!xmlAttributeErrors.ContainsKey(e.Attr.Name))
                    xmlAttributeErrors[e.Attr.Name] = e.Attr.Name;
        }

        /// <summary>
        /// Evento que é disparado quando o Serializador do XML encontra um nó desconhecido.
        /// Se o esse erro está aparecendo pela primeira vez ele grava na lista de erros.
        /// </summary>
        static void serializer_UnknownNode(object sender, XmlNodeEventArgs e)
        {
            if (xmlAttributeErrors != null)
                if (!xmlAttributeErrors.ContainsKey(e.Name))
                    xmlAttributeErrors[e.Name] = e.Name;
        }

        #endregion

    }
}
