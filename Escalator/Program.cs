using System.Diagnostics;
using System.Xml.Linq;
using NetEti.ApplicationEnvironment;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

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
    /// 04.08.2023 Erik Nagel: Im Zuge der Migration auf .Net 7 komplett überarbeitet.
    /// </remarks>
    internal static class Program
    {
        /// <summary>
        /// Haupteinstiegspunkt der Anwendung.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            int escalationCounter = EvaluateParametersOrDie(
                out string treeInfo, out string nodeInfo, out string position, out string parameterFile);

            bool paraByFile = true; // hier immer , aus dokumentatorischen Gründen werden beide Zweige gezeigt.
            if (paraByFile)
            {

                CallSubWorker(escalationCounter, treeInfo, nodeInfo, position, parameterFile);
            }
            else
            {
                CallSubWorker(escalationCounter, treeInfo, nodeInfo, position, args); // hier nie
            }
        }

        private static void CallSubWorker(
            int escalationCounter, string treeInfo, string nodeInfo, string position, string parameterFile)
        {
            XDocument? xmlDoc = null;
            if (File.Exists(parameterFile))
            {
                xmlDoc = XDocument.Load(parameterFile);
            }
            //File.Delete(parameterFile);
            XElement? para = xmlDoc?.Descendants("Parameters").First();
            var subWorkers = from item in para?.Elements("SubWorkers").Elements() select item;
            foreach (XElement subWorker in subWorkers)
            {
                var runCounterCandidate = subWorker.Attributes("RunCounter").FirstOrDefault();
                int runCounter = 0;
                if (runCounterCandidate != null)
                {
                    Int32.TryParse(runCounterCandidate.Value, out runCounter);
                }
                bool transportByFile = false;
                var subWorkerPara = subWorker.Element("Parameters");
                if (subWorkerPara != null)
                {
                    var xVar = subWorkerPara.Attributes("Transport").FirstOrDefault();
                    transportByFile = xVar == null ? false : xVar.Value.ToLower() == "file" ? true : false;
                }
                if (escalationCounter < 0 && Math.Abs(escalationCounter) >= runCounter || runCounter == escalationCounter)
                {
                    string? subWorkerPath = subWorker.Element("PhysicalPath")?.Value;
                    if (subWorkerPath != null && subWorkerPath.ToLower() != "#quiet#")
                    {
                        exec(subWorkerPath, escalationCounter, treeInfo, nodeInfo, position, subWorkerPara,
                            transportByFile, parameterFile);
                    }
                }
            }
        }

        private static void CallSubWorker(int escalationCounter, string treeInfo, string nodeInfo, string position, string[] args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Hier wird der externe Arbeitsprozess ausgeführt.
        /// In den TreeParameters oder SlaveParameters (beim Konstruktor übergeben)
        /// enthaltene Pipes ('|') werden beim Aufruf des Workers als Leerzeichen zwischen
        /// mehreren Kommandozeilenparametern interpretiert.
        /// </summary>
        /// <param name="workerPath">Dateipfad der auszuführenden Exe.</param>
        /// <param name="escalationCounter">Aufrufzähler (1 bis n oder -n bis -1).
        /// Bei negativem Wert wird der Worker resettet (Fehler behoben).
        /// Der Absolutwert zeigt die letzte Eskalationsstufe.</param>
        /// <param name="treeInfo">Für den gesamten Tree gültige Parameter oder null.</param>
        /// <param name="nodeInfo">Id des Knotens, der diesen Worker besitzt.</param>
        /// <param name="position">Position des übergeordneten Controls.</param>
        /// <param name="para">String mit Übergabeparametern für den Worker</param>
        /// <param name="transportByFile">Bei True werden die Parameter über eie XML-Datei übergeben,
        /// ansonsten über die Kommandozeile.</param>
        /// <param name="parameterFile">Dateipfad der XML-Datei mit den ursprünglich an den Executor
        /// übergebenen Parametern.</param>
        private static void exec(
            string workerPath, int escalationCounter, string treeInfo, string nodeInfo,
            string position, XElement? para, bool transportByFile, string parameterFile)
        {
            Process externalProcess = new Process();
            string countString = "EscalationCounter=" + escalationCounter.ToString();
            externalProcess.StartInfo.FileName = workerPath;
            externalProcess.StartInfo.Arguments = countString + " " + treeInfo + " " + nodeInfo + " " + position;
            string konvertedSlaveParameters;
            if (!transportByFile)
            {
                konvertedSlaveParameters = para?.Value ?? "";
            }
            else
            {
                konvertedSlaveParameters = (para?.ToString() ?? "").Replace('\xA0', ' ').Replace('\x09', ' ');
            }
            if (!transportByFile)
            {
                externalProcess.StartInfo.Arguments += " " + konvertedSlaveParameters;
            }
            else
            {
                string parameterFilePath = Path.Combine(Path.GetDirectoryName(parameterFile) ?? "",
                  Path.GetFileNameWithoutExtension(parameterFile) + "_" + Path.GetFileNameWithoutExtension(workerPath) + ".para");
                string[] lines = { "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                                    konvertedSlaveParameters
                                 };
                System.IO.File.WriteAllLines(parameterFilePath, lines);
                externalProcess.StartInfo.Arguments += " -ParameterFile=\"" + parameterFilePath +"\"";
            }
            externalProcess.StartInfo.Arguments
                = Regex.Replace(externalProcess.StartInfo.Arguments,
                  "\\s+(?=([^\"]*\"[^\"]*\")*[^\"]*$)", " ", RegexOptions.IgnoreCase).Trim();
            externalProcess.Start();
        }

        private static int EvaluateParametersOrDie(
            out string treeInfo, out string nodeInfo, out string position, out string parameterFile)
        {
            CommandLineAccess commandLineAccess = new();

            string? tmpStr = commandLineAccess.GetStringValue("EscalationCounter", "0");
            if (!Int32.TryParse(tmpStr, out int escalationCounter))
                Die<string>("Es muss ein numerischer EscalationCounter übergeben werden.", commandLineAccess.CommandLine);

            treeInfo = commandLineAccess.GetStringValue("Vishnu.TreeInfo", "") ?? string.Empty;
            if (!String.IsNullOrEmpty(treeInfo))
            {
                treeInfo = "-Vishnu.TreeInfo=" + "\"" + treeInfo + "\"";
            }

            nodeInfo = commandLineAccess.GetStringValue("Vishnu.NodeInfo", "") ?? string.Empty;
            if (!String.IsNullOrEmpty(nodeInfo))
            {
                nodeInfo = "-Vishnu.NodeInfo=" + "\"" + nodeInfo + "\"";
            }

            position = "-Position=" + "\"" + (commandLineAccess.GetStringValue("Position", "") ?? "") + "\"";

            parameterFile = commandLineAccess.GetStringValue("ParameterFile", "") ?? string.Empty;

            return escalationCounter;
        }

        private static T Die<T>(string? message, string? commandLine = null)
        {
            string usage = "Syntax:"
                + Environment.NewLine
                + "\t-EscalationCounter={-n;+n} (negativ: Ursache behoben)"
                + Environment.NewLine
                + "\t[-ParameterFile=<Pfad zur Datei mit Parametern>]"
                + Environment.NewLine
                + "\t[-Vishnu.TreeInfo=<wird von Vishnu gesetzt>]"
                + Environment.NewLine
                + "\t[-Vishnu.NodeInfo=<wird von Vishnu gesetzt>]"
                + Environment.NewLine
                + "\t[-Position=<X;Y>]";
            if (commandLine != null)
            {
                usage = "Kommandozeile: " + commandLine + Environment.NewLine + usage;
            }
            MessageBox.Show(message + Environment.NewLine + usage);
            throw new ArgumentException(message + Environment.NewLine + usage);
        }
    }
}