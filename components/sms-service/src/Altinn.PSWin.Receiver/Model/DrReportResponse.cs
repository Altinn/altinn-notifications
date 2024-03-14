using System.IO;
using System.Xml.Serialization;
using System.Xml;

namespace LinkMobility.PSWin.Receiver.Model
{
    /// <summary>
    /// Delivery Report message class
    /// </summary>
    public class DRReportResponse
    {
        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(DRReportResponse));

        /// <summary>
        /// Constructor for a delivery report message
        /// </summary>
        public DRReportResponse(string id, ReportResponseStatus status)
        {
            Id = id;
            Status = status;
        }

        /// <summary>
        /// Constructor for a delivery report message
        /// </summary>
        public DRReportResponse()
        {                
        }

        /// <summary>
        /// The id of the DR
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Report response status
        /// </summary>
        public ReportResponseStatus Status { get; set; }

        /// <summary>
        /// Serializes the response to an XML string
        /// </summary>
        public string SerializeToXmlString()
        {
            StringWriter stringWriter = new StringWriter();
            using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter))
            {
                _serializer.Serialize(xmlWriter, this);
                return stringWriter.ToString();
            }
        }
    }


    /// <summary>
    /// Enum describing all report response states
    /// </summary>
    public enum ReportResponseStatus
    {
        /// <summary>
        /// Report was successfully received
        /// </summary>
        OK,

        /// <summary>
        /// Report was not successfully received
        /// </summary>
        FAIL
    }
}
