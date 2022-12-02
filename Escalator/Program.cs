using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;

namespace Escalator
{
    /// <summary>
    /// Ruft weitere Worker (Exen) mit Kommandozeilen-Parametern auf.
    /// Berücksichtigt je nach Aufruf-Zähler unterschiedliche Worker
    /// (Eskalationsstufen), z.B. ConsoleMessageBox, MicroMailer, ...
    /// </summary>
    /// <remarks>
    /// File: Program.cs
    /// Autor: Erik Nagel
    ///
    /// 16.04.2016 Erik Nagel: Erstellt.
    /// 03.07.2021 Erik Nagel: Parameter "#quiet#" implementiert.
    /// </remarks>
    public class Program
    {
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            int aufrufCounter = Convert.ToInt32(args[0]);
            string treeInfo = args[1];
            string nodeId = args[2];
            args = args.Skip(3).ToArray();
            bool paraByFile = true; // hier immer , aus dokumentatorischen Gründen werden beide Zweige gezeigt.
            if (!paraByFile)
            {
                showMessageFromArgs(aufrufCounter, treeInfo, nodeId, args); // hier nie
            }
            else
            {
                showMessageFromFile(aufrufCounter, treeInfo, nodeId, args[0]);
            }
        }

        private static void showMessageFromFile(int aufrufCounter, string treeInfo, string nodeId, string parameterFile)
        {
            string aufrufInfo = buildAufrufInfo(aufrufCounter);
            string msg = aufrufInfo;
            string delim = Environment.NewLine;
            XDocument xmlDoc = null;
            if (File.Exists(parameterFile))
            {
                xmlDoc = XDocument.Load(parameterFile);
            }
            //File.Delete(parameterFile);
            XElement para = xmlDoc.Descendants("Parameters").First();
            var subWorkers = from item in para.Elements("SubWorkers").Elements() select item;
            foreach (XElement subWorker in subWorkers)
            {
                var runCounterCandidate = subWorker.Attributes("RunCounter").FirstOrDefault();
                int runCounter = 0;
                if (runCounterCandidate != null)
                {
                    Int32.TryParse(runCounterCandidate.Value, out runCounter);
                }
                bool transportByFile = false;
                string subWorkerParaString = "";
                var subWorkerPara = subWorker.Element("Parameters");
                if (subWorkerPara != null)
                {
                    var xVar = subWorkerPara.Attributes("Transport").FirstOrDefault();
                    transportByFile = xVar == null ? false : xVar.Value.ToLower() == "file" ? true : false;
                    subWorkerParaString = "\"" + subWorkerPara.Value.Replace("|", "\" \"") + "\"";
                    // Der 1. Replace ersetzt Leerzeichen durch geschützte Leerzeichen (255).
                }
                if (aufrufCounter < 0 && Math.Abs(aufrufCounter) >= runCounter || runCounter == aufrufCounter)
                {
                    string subWorkerPath = subWorker.Element("PhysicalPath").Value;
                    if (subWorkerPath != null && subWorkerPath.ToLower() != "#quiet#")
                    {
                        exec(subWorkerPath, aufrufCounter, treeInfo, nodeId, subWorkerParaString, transportByFile, parameterFile);
                    }
                }
            }
        }

        private static void showMessageFromArgs(int aufrufCounter, string treeInfo, string nodeId, string[] args)
        {
            string aufrufInfo = buildAufrufInfo(aufrufCounter);
            string msg = aufrufInfo;
            string delim = Environment.NewLine;
            for (int i = 0; i < args.Length - 1; i++)
            {
                msg += delim + args[i];
                delim = Environment.NewLine;
            }
            MessageBoxIcon icon = MessageBoxIcon.Information;
            if (msg != "") // letzter (User-)Parameter wird zur Überschrift.
            {
                string header = args[args.Length - 1];
                if (aufrufInfo.StartsWith("Das Problem ist behoben"))
                {
                    header = "Entwarnung";
                }
                if (header.ToUpper().Contains("ERROR") || header.ToUpper().Contains("FEHLER") || header.ToUpper().Contains("EXCEPTION"))
                {
                    icon = MessageBoxIcon.Error;
                }
                MessageBox.Show(msg, header, MessageBoxButtons.OK, icon);
            }
            else // letzter (User-)Parameter ist die Meldung
            {
                if (args.Length > 0)
                {
                    msg = args[args.Length - 1];
                    MessageBox.Show(msg, "Info");
                    string header = "Information";
                    if (msg.ToUpper().Contains("ERROR") || msg.ToUpper().Contains("FEHLER") || msg.ToUpper().Contains("EXCEPTION"))
                    {
                        icon = MessageBoxIcon.Error;
                        header = "Fehler";
                    }
                    MessageBox.Show(msg, header, MessageBoxButtons.OK, icon);
                }
            }
        }

        private static string buildAufrufInfo(int aufrufCounter)
        {
            if (aufrufCounter < 0)
            {
                return "Die frühere Meldung trifft nicht mehr zu. Die ursprüngliche Meldung war:";
            }
            else
            {
                return aufrufCounter.ToString() + ". Achtung";
            }
        }

        /// <summary>
        /// Hier wird der externe Arbeitsprozess ausgeführt.
        /// In den TreeParameters oder SlaveParameters (beim Konstruktor übergeben)
        /// enthaltene Pipes ('|') werden beim Aufruf des Workers als Leerzeichen zwischen
        /// mehreren Kommandozeilenparametern interpretiert.
        /// </summary>
        /// <param name="workerPath">Dateipfad der auszuführenden Exe.</param>
        /// <param name="callCounter">Aufrufzähler (1-n oder x*-1). Bei negativem Wert wird der Worker resettet (Fehler behoben). Der Absolutwert zeigt die letzte Eskalationsstufe.</param>
        /// <param name="treeInfo">Für den gesamten Tree gültige Parameter oder null.</param>
        /// <param name="nodeId">Id des Knotens, der diesen Worker besitzt.</param>
        /// <param name="para">String mit Übergabeparametern für den Worker</param>
        /// <param name="transportByFile">Bei True werden die Parameter über eie XML-Datei übergeben, ansonsten über die Kommandozeile.</param>
        /// <param name="parameterFile">Dateipfad der XML-Datei mit den ursprünglich an den Executor übergebenen Parametern.</param>
        private static void exec(string workerPath, int callCounter, string treeInfo, string nodeId, string para, bool transportByFile, string parameterFile)
        {
            Process externalProcess = new Process();
            string countString = callCounter.ToString();
            string konvertedSlaveParameters = para;
            externalProcess.StartInfo.FileName = workerPath;
            externalProcess.StartInfo.Arguments = countString
                                     + " \"" + treeInfo.Replace("|", "\" \"")
                                     + "\" \"" + nodeId;
            if (!transportByFile)
            {
                externalProcess.StartInfo.Arguments += "\" " + konvertedSlaveParameters.Replace("|", "\" \"");
            }
            else
            {
                string parameterFilePath = Path.Combine(Path.GetDirectoryName(parameterFile),
                  Path.GetFileNameWithoutExtension(parameterFile) + "_" + Path.GetFileNameWithoutExtension(workerPath) + ".para");
                string[] lines = { "<?xml version=\"1.0\" encoding=\"utf-8\"?>", konvertedSlaveParameters };
                System.IO.File.WriteAllLines(parameterFilePath, lines);
                externalProcess.StartInfo.Arguments += "\" " + parameterFilePath.Replace("|", "\" \"");
            }
            externalProcess.Start();
        }

    }
}
