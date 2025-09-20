using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Escalator
{
    /// <summary>
    /// Klasse, die einen strukturierten Parameter für den Escalator repräsentiert.
    /// Wird beim Aufruf durch Vishnu in einer externen Xml- oder Json-Datei gespeichert.
    /// </summary>
    [XmlRoot("Parameters", Namespace = "")]
    public class StructuredParameters
    {
        /// <summary>
        /// Liste von SubWorkern des aktuellen Workers.
        /// </summary>
        public SubWorkersContainer SubWorkers { get; set; }

        /// <summary>
        /// Standard Konstruktor.
        /// </summary>
        public StructuredParameters()
        {
            SubWorkers = new SubWorkersContainer();
        }
    }

    /// <summary>
    /// Enthält eine Liste von SubWorkern des aktuellen Workers.
    /// </summary>
    public class SubWorkersContainer
    {
        // Im JSON ist das Array in einer Eigenschaft "SubWorker" eingebettet.
        /// <summary>
        /// Die eigentliche Liste.
        /// </summary>
        [JsonPropertyName("SubWorker")]
        [XmlElement("SubWorker")]
        public System.Collections.Generic.List<SubWorker> SubWorkersList { get; set; }

        /// <summary>
        /// Konstruktor - initialisiert die Liste.
        /// </summary>
        public SubWorkersContainer()
        {
            this.SubWorkersList = new List<SubWorker>();
        }
    }

    /// <summary>
    /// Kapselt einen SubWorker für einen Worker.
    /// </summary>
    public class SubWorker
    {
        /// <summary>
        /// Der spezifische Schweregrad, ab dem die SubWorker-Dll auslöst.
        /// </summary>
        [XmlAttribute("RunCounter")]
        [JsonPropertyName("@RunCounter")]
        public string? RunCounter { get; set; }

        /// <summary>
        /// Der physiche Path zur SubWorker-Dll.
        /// </summary>
        [JsonPropertyName("PhysicalPath")]
        public string? PhysicalPath { get; set; }

        /// <summary>
        /// Optionale SubWorker-Parameter.
        /// </summary>
        [JsonPropertyName("Parameters")]
        public string? Parameters { get; set; }
    }

}
