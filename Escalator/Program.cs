using System.Diagnostics;
using NetEti.ApplicationEnvironment;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using NetEti.ObjectSerializer;
using Vishnu.Interchange;

namespace Escalator
{
    /// <summary>
    /// Ruft weitere Worker (EXE) mit Kommandozeilen-Parametern auf.
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
    /// 26.04.2025 Erik Nagel: Unterstützung für Json implementiert.
    /// </remarks>
    internal static class Program
    {
        /// <summary>
        /// Haupteinstiegspunkt der Anwendung.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            int escalationCounter = EvaluateUnstructuredParametersOrDie(out string treeInfo,
                out string nodeInfo, out string position, out string parameterFile, out string handle);
            StructuredParameters subWorkersPara
                = EvaluateStructuredParametersOrDie(parameterFile);
            CallSubWorkers(treeInfo, nodeInfo, position, subWorkersPara, escalationCounter, handle);
        }

        private static void CallSubWorkers(string treeInfo, string nodeInfo, string position,
            StructuredParameters subWorkersPara, int escalationCounter, string handle)
        {
            foreach (SubWorker subWorker in subWorkersPara.SubWorkers.SubWorkersList)
            {
                int runCounter = Convert.ToInt32(subWorker.RunCounter);
                if (escalationCounter < 0 && Math.Abs(escalationCounter) >= runCounter || runCounter == escalationCounter)
                {
                    string? subWorkerPath = subWorker.PhysicalPath;

                    if (subWorkerPath != null && subWorkerPath.ToLower() != "#quiet#")
                    {
                        subWorkerPath = VishnuAssemblyLoader.GetResolvedAssemblyPath(subWorkerPath, handle);
                        ExecuteWorker(subWorkerPath, escalationCounter, treeInfo, nodeInfo, position, subWorker.Parameters);
                    }
                }
            }
        }

        /// <summary>
        /// Hier wird der externe Arbeitsprozess ausgeführt.
        /// In den TreeParameters oder SlaveParameters (beim Konstruktor übergeben)
        /// enthaltene Pipes ('|') werden beim Aufruf des Workers als Leerzeichen zwischen
        /// mehreren Kommandozeilenparametern interpretiert.
        /// </summary>
        /// <param name="workerPath">Dateipfad der auszuführenden Exe.</param>
        /// <param name="escalationCounter">Aufrufzähler (1 bis n oder -n bis -1).
        /// Bei negativem Wert wird der Worker resettet (Fehler behoben).</param>
        /// <param name="treeInfo">Für den gesamten Tree gültige Parameter oder null.</param>
        /// <param name="nodeInfo">Id des Knotens, der diesen Worker besitzt.</param>
        /// <param name="position">Position des übergeordneten Controls.</param>
        /// <param name="parameters">String mit Übergabeparametern für den Worker oder null.</param>
        private static void ExecuteWorker(
            string workerPath, int escalationCounter, string treeInfo, string nodeInfo,
            string position, string? parameters)
        {
            string countString = "EscalationCounter=" + escalationCounter.ToString();
            string konvertedSlaveParameters = String.Empty;
            if (parameters != null)
            {
                konvertedSlaveParameters = parameters.Replace('\xA0', ' ').Replace('\x09', ' ');
            }
            Process externalProcess = new Process();
            externalProcess.StartInfo.FileName = workerPath;
            externalProcess.StartInfo.Arguments = countString + " " + treeInfo + " "
                + nodeInfo + " " + position + " " + konvertedSlaveParameters;
            externalProcess.StartInfo.Arguments
                = Regex.Replace(externalProcess.StartInfo.Arguments,
                  "\\s+(?=([^\"]*\"[^\"]*\")*[^\"]*$)", " ", RegexOptions.IgnoreCase).Trim();
            externalProcess.Start();
        }

        private static int EvaluateUnstructuredParametersOrDie(
            out string treeInfo, out string nodeInfo, out string position, out string parameterFile, out string handle)
        {
            CommandLineAccess commandLineAccess = new();

            string? tmpStr = commandLineAccess.GetStringValue("DebugMode", "false");
            if (tmpStr?.ToLower().Equals("true") == true)
            {
                if (!Debugger.IsAttached) Debugger.Launch();
            }

            tmpStr = commandLineAccess.GetStringValue("EscalationCounter", "0");
            if (!Int32.TryParse(tmpStr, out int escalationCounter))
            {
                string message = "Es muss ein numerischer EscalationCounter übergeben werden."
                    + Environment.NewLine + Syntax();
                MessageBox.Show(message);
                throw new ArgumentException(message);
            }
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
            handle = commandLineAccess.GetStringValue("Handle", "") ?? string.Empty;

            return escalationCounter;
        }

        private static StructuredParameters EvaluateStructuredParametersOrDie(string parameterFile)
        {
            StructuredParameters? subWorkersPara = null;
            if (File.Exists(parameterFile))
            {
                string parameterString = File.ReadAllText(parameterFile);
                if (parameterString.Trim().StartsWith('{'))
                {
                    subWorkersPara =
                        SerializationUtility.DeserializeFromJson<StructuredParameters>(parameterString);
                }
                else
                {
                    if (parameterString.Trim().StartsWith('<'))
                    {
                        subWorkersPara =
                            SerializationUtility.DeserializeFromXml<StructuredParameters>(parameterString);
                    }
                    else
                    {
                        string message =
                            "Die SubWorker-Parameter haben kein bekanntes Format. Unterstützt werden Xml und Json."
                            + Environment.NewLine + Syntax();
                        MessageBox.Show(message);
                        throw new ArgumentException(message);
                    }
                }
            }
            if (subWorkersPara == null)
            {
                string message = "Die SubWorker-Parameter sind null." + Environment.NewLine + Syntax();
                MessageBox.Show(message);
                throw new ArgumentException(message);
            }
            return subWorkersPara;
        }

        private static string Syntax()
        {
            string message = "Syntax:"
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
            return message;
        }
    }
}