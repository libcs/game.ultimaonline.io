using System;
using System.Xml;

namespace UltimaOnline.Accounting
{
    public class AccountComment
    {
        string _Content;

        /// <summary>
        /// A string representing who added this comment.
        /// </summary>
        public string AddedBy { get; }

        /// <summary>
        /// Gets or sets the body of this comment. Setting this value will reset LastModified.
        /// </summary>
        public string Content
        {
            get => _Content;
            set { _Content = value; LastModified = DateTime.UtcNow; }
        }

        /// <summary>
        /// The date and time when this account was last modified -or- the comment creation time, if never modified.
        /// </summary>
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// Constructs a new AccountComment instance.
        /// </summary>
        /// <param name="addedBy">Initial AddedBy value.</param>
        /// <param name="content">Initial Content value.</param>
        public AccountComment(string addedBy, string content)
        {
            AddedBy = addedBy;
            _Content = content;
            LastModified = DateTime.UtcNow;
        }

        /// <summary>
        /// Deserializes an AccountComment instance from an xml element.
        /// </summary>
        /// <param name="node">The XmlElement instance from which to deserialize.</param>
        public AccountComment(XmlElement node)
        {
            AddedBy = Utility.GetAttribute(node, "addedBy", "empty");
            LastModified = Utility.GetXMLDateTime(Utility.GetAttribute(node, "lastModified"), DateTime.UtcNow);
            _Content = Utility.GetText(node, string.Empty);
        }

        /// <summary>
        /// Serializes this AccountComment instance to an XmlTextWriter.
        /// </summary>
        /// <param name="w">The XmlTextWriter instance from which to serialize.</param>
        public void Save(XmlTextWriter w)
        {
            w.WriteStartElement("comment");
            w.WriteAttributeString("addedBy", AddedBy);
            w.WriteAttributeString("lastModified", XmlConvert.ToString(LastModified, XmlDateTimeSerializationMode.Utc));
            w.WriteString(_Content);
            w.WriteEndElement();
        }
    }
}