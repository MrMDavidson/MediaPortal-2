#region Copyright (C) 2007-2010 Team MediaPortal

/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using MediaPortal.Core.MediaManagement.MLQueries;

namespace MediaPortal.Core.MediaManagement
{
  /// <summary>
  /// Represents a group of results in a media library query, identified by a group name.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Note: This class is serialized/deserialized by the <see cref="XmlSerializer"/>.
  /// If changed, this has to be taken into consideration.
  /// </para>
  /// </remarks>
  public class MLQueryResultGroup
  {
    protected string _groupName;
    protected int _numItemsInGroup;
    protected IFilter _additionalFilter;

    // We could use some cache for this instance, if we would have one...
    [ThreadStatic]
    protected static XmlSerializer _xmlSerializer = null; // Lazy initialized
    
    public MLQueryResultGroup(string groupName, int numItemsInGroup, IFilter additionalFilter)
    {
      _groupName = groupName;
      _numItemsInGroup = numItemsInGroup;
      _additionalFilter = additionalFilter;
    }

    [XmlIgnore]
    public string GroupName
    {
      get { return _groupName; }
    }

    [XmlIgnore]
    public int NumItemsInGroup
    {
      get { return _numItemsInGroup; }
      set { _numItemsInGroup = value; }
    }

    [XmlIgnore]
    public IFilter AdditionalFilter
    {
      get { return _additionalFilter; }
    }

    /// <summary>
    /// Serializes this value group instance to XML.
    /// </summary>
    /// <returns>String containing an XML fragment with this instance's data.</returns>
    public string Serialize()
    {
      XmlSerializer xs = GetOrCreateXMLSerializer();
      lock (xs)
      {
        StringBuilder sb = new StringBuilder(); // Will contain the data, formatted as XML
        using (XmlWriter writer = XmlWriter.Create(sb, new XmlWriterSettings {OmitXmlDeclaration = true}))
          xs.Serialize(writer, this);
        return sb.ToString();
      }
    }

    /// <summary>
    /// Serializes this value group instance to the given <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">Writer to write the XML serialization to.</param>
    public void Serialize(XmlWriter writer)
    {
      XmlSerializer xs = GetOrCreateXMLSerializer();
      lock (xs)
        xs.Serialize(writer, this);
    }

    /// <summary>
    /// Deserializes a value group instance from a given XML fragment.
    /// </summary>
    /// <param name="str">XML fragment containing a serialized value group instance.</param>
    /// <returns>Deserialized instance.</returns>
    public static MLQueryResultGroup Deserialize(string str)
    {
      XmlSerializer xs = GetOrCreateXMLSerializer();
      lock (xs)
        using (StringReader reader = new StringReader(str))
          return xs.Deserialize(reader) as MLQueryResultGroup;
    }

    /// <summary>
    /// Deserializes a value group instance from a given <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">XML reader containing a serialized value group instance.</param>
    /// <returns>Deserialized instance.</returns>
    public static MLQueryResultGroup Deserialize(XmlReader reader)
    {
      XmlSerializer xs = GetOrCreateXMLSerializer();
      lock (xs)
        return xs.Deserialize(reader) as MLQueryResultGroup;
    }

    #region Additional members for the XML serialization

    internal MLQueryResultGroup() { }

    protected static XmlSerializer GetOrCreateXMLSerializer()
    {
      if (_xmlSerializer == null)
        _xmlSerializer = new XmlSerializer(typeof(MLQueryResultGroup), new Type[] {typeof(FilterWrapper)});
      return _xmlSerializer;
    }

    /// <summary>
    /// For internal use of the XML serialization system only.
    /// </summary>
    [XmlAttribute("Name")]
    public string XML_GroupName
    {
      get { return _groupName; }
      set { _groupName = value; }
    }

    /// <summary>
    /// For internal use of the XML serialization system only.
    /// </summary>
    [XmlAttribute("NumItems")]
    public int XML_NumItems
    {
      get { return _numItemsInGroup; }
      set { _numItemsInGroup = value; }
    }

    /// <summary>
    /// For internal use of the XML serialization system only.
    /// </summary>
    [XmlElement("AdditionalFilter")]
    public FilterWrapper XML_AdditionalFilter
    {
      get { return new FilterWrapper(_additionalFilter); }
      set { _additionalFilter = value.Filter; }
    }

    #endregion
  }
}