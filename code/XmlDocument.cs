using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Schema;
using System.Xml.XPath;

namespace System.Xml
{
	// Token: 0x02000252 RID: 594
	[Nullable(0)]
	[NullableContext(1)]
	public class XmlDocument : XmlNode
	{
		// Token: 0x06001932 RID: 6450 RVA: 0x000E8C74 File Offset: 0x000E7874
		public XmlDocument() : this(new XmlImplementation())
		{
		}

		// Token: 0x06001933 RID: 6451 RVA: 0x000E8C81 File Offset: 0x000E7881
		public XmlDocument(XmlNameTable nt) : this(new XmlImplementation(nt))
		{
		}

		// Token: 0x06001934 RID: 6452 RVA: 0x000E8C90 File Offset: 0x000E7890
		protected internal XmlDocument(XmlImplementation imp)
		{
			this._implementation = imp;
			this._domNameTable = new DomNameTable(this);
			this.strXmlns = "xmlns";
			this.strXml = "xml";
			this.strReservedXmlns = "http://www.w3.org/2000/xmlns/";
			this.strReservedXml = "http://www.w3.org/XML/1998/namespace";
			this.baseURI = string.Empty;
			this.objLock = new object();
			if (imp.NameTable.GetType() == typeof(NameTable))
			{
				NameTable nameTable = (NameTable)imp.NameTable;
				this.strDocumentName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[0].Item1, XmlDocument.s_nameTableSeeds[0].Item2);
				this.strDocumentFragmentName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[1].Item1, XmlDocument.s_nameTableSeeds[1].Item2);
				this.strCommentName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[2].Item1, XmlDocument.s_nameTableSeeds[2].Item2);
				this.strTextName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[3].Item1, XmlDocument.s_nameTableSeeds[3].Item2);
				this.strCDataSectionName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[4].Item1, XmlDocument.s_nameTableSeeds[4].Item2);
				this.strEntityName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[5].Item1, XmlDocument.s_nameTableSeeds[5].Item2);
				this.strID = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[6].Item1, XmlDocument.s_nameTableSeeds[6].Item2);
				this.strNonSignificantWhitespaceName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[11].Item1, XmlDocument.s_nameTableSeeds[11].Item2);
				this.strSignificantWhitespaceName = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[12].Item1, XmlDocument.s_nameTableSeeds[12].Item2);
				this.strXmlns = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[7].Item1, XmlDocument.s_nameTableSeeds[7].Item2);
				this.strXml = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[8].Item1, XmlDocument.s_nameTableSeeds[8].Item2);
				this.strSpace = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[9].Item1, XmlDocument.s_nameTableSeeds[9].Item2);
				this.strLang = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[10].Item1, XmlDocument.s_nameTableSeeds[10].Item2);
				this.strReservedXmlns = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[13].Item1, XmlDocument.s_nameTableSeeds[13].Item2);
				this.strReservedXml = nameTable.GetOrAddEntry(XmlDocument.s_nameTableSeeds[14].Item1, XmlDocument.s_nameTableSeeds[14].Item2);
				return;
			}
			XmlNameTable nameTable2 = imp.NameTable;
			this.strDocumentName = nameTable2.Add("#document");
			this.strDocumentFragmentName = nameTable2.Add("#document-fragment");
			this.strCommentName = nameTable2.Add("#comment");
			this.strTextName = nameTable2.Add("#text");
			this.strCDataSectionName = nameTable2.Add("#cdata-section");
			this.strEntityName = nameTable2.Add("#entity");
			this.strID = nameTable2.Add("id");
			this.strNonSignificantWhitespaceName = nameTable2.Add("#whitespace");
			this.strSignificantWhitespaceName = nameTable2.Add("#significant-whitespace");
			this.strXmlns = nameTable2.Add("xmlns");
			this.strXml = nameTable2.Add("xml");
			this.strSpace = nameTable2.Add("space");
			this.strLang = nameTable2.Add("lang");
			this.strReservedXmlns = nameTable2.Add("http://www.w3.org/2000/xmlns/");
			this.strReservedXml = nameTable2.Add("http://www.w3.org/XML/1998/namespace");
		}

		// Token: 0x170007D7 RID: 2007
		// (get) Token: 0x06001935 RID: 6453 RVA: 0x000E90CA File Offset: 0x000E7CCA
		// (set) Token: 0x06001936 RID: 6454 RVA: 0x000E90D2 File Offset: 0x000E7CD2
		[Nullable(2)]
		internal SchemaInfo DtdSchemaInfo
		{
			get
			{
				return this._schemaInfo;
			}
			set
			{
				this._schemaInfo = value;
			}
		}

		// Token: 0x06001937 RID: 6455 RVA: 0x000E90DC File Offset: 0x000E7CDC
		internal static void CheckName(string name)
		{
			int num = ValidateNames.ParseNmtoken(name, 0);
			if (num < name.Length)
			{
				throw new XmlException(SR.Xml_BadNameChar, XmlException.BuildCharExceptionArgs(name, num));
			}
		}

		// Token: 0x06001938 RID: 6456 RVA: 0x000E910C File Offset: 0x000E7D0C
		internal XmlName AddXmlName(string prefix, string localName, string namespaceURI, IXmlSchemaInfo schemaInfo)
		{
			return this._domNameTable.AddName(prefix, localName, namespaceURI, schemaInfo);
		}

		// Token: 0x06001939 RID: 6457 RVA: 0x000E912C File Offset: 0x000E7D2C
		internal XmlName GetXmlName(string prefix, string localName, string namespaceURI, IXmlSchemaInfo schemaInfo)
		{
			return this._domNameTable.GetName(prefix, localName, namespaceURI, schemaInfo);
		}

		// Token: 0x0600193A RID: 6458 RVA: 0x000E914C File Offset: 0x000E7D4C
		internal XmlName AddAttrXmlName(string prefix, string localName, string namespaceURI, IXmlSchemaInfo schemaInfo)
		{
			XmlName xmlName = this.AddXmlName(prefix, localName, namespaceURI, schemaInfo);
			if (!this.IsLoading)
			{
				object prefix2 = xmlName.Prefix;
				object namespaceURI2 = xmlName.NamespaceURI;
				object localName2 = xmlName.LocalName;
				if ((prefix2 == this.strXmlns || (xmlName.Prefix.Length == 0 && localName2 == this.strXmlns)) ^ namespaceURI2 == this.strReservedXmlns)
				{
					throw new ArgumentException(SR.Format(SR.Xdom_Attr_Reserved_XmlNS, namespaceURI));
				}
			}
			return xmlName;
		}

		// Token: 0x0600193B RID: 6459 RVA: 0x000E91C5 File Offset: 0x000E7DC5
		internal bool AddIdInfo(XmlName eleName, XmlName attrName)
		{
			if (this._htElementIDAttrDecl == null || this._htElementIDAttrDecl[eleName] == null)
			{
				if (this._htElementIDAttrDecl == null)
				{
					this._htElementIDAttrDecl = new Hashtable();
				}
				this._htElementIDAttrDecl.Add(eleName, attrName);
				return true;
			}
			return false;
		}

		// Token: 0x0600193C RID: 6460 RVA: 0x000E9200 File Offset: 0x000E7E00
		private XmlName GetIDInfoByElement_(XmlName eleName)
		{
			XmlName xmlName = this.GetXmlName(eleName.Prefix, eleName.LocalName, string.Empty, null);
			if (xmlName != null)
			{
				return (XmlName)this._htElementIDAttrDecl[xmlName];
			}
			return null;
		}

		// Token: 0x0600193D RID: 6461 RVA: 0x000E923C File Offset: 0x000E7E3C
		internal XmlName GetIDInfoByElement(XmlName eleName)
		{
			if (this._htElementIDAttrDecl == null)
			{
				return null;
			}
			return this.GetIDInfoByElement_(eleName);
		}

		// Token: 0x0600193E RID: 6462 RVA: 0x000E9250 File Offset: 0x000E7E50
		private WeakReference GetElement(ArrayList elementList, XmlElement elem)
		{
			ArrayList arrayList = new ArrayList();
			foreach (object obj in elementList)
			{
				WeakReference weakReference = (WeakReference)obj;
				if (!weakReference.IsAlive)
				{
					arrayList.Add(weakReference);
				}
				else if ((XmlElement)weakReference.Target == elem)
				{
					return weakReference;
				}
			}
			foreach (object obj2 in arrayList)
			{
				WeakReference obj3 = (WeakReference)obj2;
				elementList.Remove(obj3);
			}
			return null;
		}

		// Token: 0x0600193F RID: 6463 RVA: 0x000E931C File Offset: 0x000E7F1C
		internal void AddElementWithId(string id, XmlElement elem)
		{
			if (this._htElementIdMap == null || !this._htElementIdMap.Contains(id))
			{
				if (this._htElementIdMap == null)
				{
					this._htElementIdMap = new Hashtable();
				}
				ArrayList arrayList = new ArrayList();
				arrayList.Add(new WeakReference(elem));
				this._htElementIdMap.Add(id, arrayList);
				return;
			}
			ArrayList arrayList2 = (ArrayList)this._htElementIdMap[id];
			if (this.GetElement(arrayList2, elem) == null)
			{
				arrayList2.Add(new WeakReference(elem));
			}
		}

		// Token: 0x06001940 RID: 6464 RVA: 0x000E939C File Offset: 0x000E7F9C
		internal void RemoveElementWithId(string id, XmlElement elem)
		{
			if (this._htElementIdMap != null && this._htElementIdMap.Contains(id))
			{
				ArrayList arrayList = (ArrayList)this._htElementIdMap[id];
				WeakReference element = this.GetElement(arrayList, elem);
				if (element != null)
				{
					arrayList.Remove(element);
					if (arrayList.Count == 0)
					{
						this._htElementIdMap.Remove(id);
					}
				}
			}
		}

		// Token: 0x06001941 RID: 6465 RVA: 0x000E93F8 File Offset: 0x000E7FF8
		public override XmlNode CloneNode(bool deep)
		{
			XmlDocument xmlDocument = this.Implementation.CreateDocument();
			xmlDocument.SetBaseURI(this.baseURI);
			if (deep)
			{
				xmlDocument.ImportChildren(this, xmlDocument, deep);
			}
			return xmlDocument;
		}

		// Token: 0x170007D8 RID: 2008
		// (get) Token: 0x06001942 RID: 6466 RVA: 0x000E942A File Offset: 0x000E802A
		public override XmlNodeType NodeType
		{
			get
			{
				return XmlNodeType.Document;
			}
		}

		// Token: 0x170007D9 RID: 2009
		// (get) Token: 0x06001943 RID: 6467 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public override XmlNode ParentNode
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x170007DA RID: 2010
		// (get) Token: 0x06001944 RID: 6468 RVA: 0x000E942E File Offset: 0x000E802E
		[Nullable(2)]
		public virtual XmlDocumentType DocumentType
		{
			[NullableContext(2)]
			get
			{
				return (XmlDocumentType)this.FindChild(XmlNodeType.DocumentType);
			}
		}

		// Token: 0x170007DB RID: 2011
		// (get) Token: 0x06001945 RID: 6469 RVA: 0x000E9440 File Offset: 0x000E8040
		[Nullable(2)]
		internal virtual XmlDeclaration Declaration
		{
			get
			{
				if (this.HasChildNodes)
				{
					return this.FirstChild as XmlDeclaration;
				}
				return null;
			}
		}

		// Token: 0x170007DC RID: 2012
		// (get) Token: 0x06001946 RID: 6470 RVA: 0x000E9464 File Offset: 0x000E8064
		public XmlImplementation Implementation
		{
			get
			{
				return this._implementation;
			}
		}

		// Token: 0x170007DD RID: 2013
		// (get) Token: 0x06001947 RID: 6471 RVA: 0x000E946C File Offset: 0x000E806C
		public override string Name
		{
			get
			{
				return this.strDocumentName;
			}
		}

		// Token: 0x170007DE RID: 2014
		// (get) Token: 0x06001948 RID: 6472 RVA: 0x000E946C File Offset: 0x000E806C
		public override string LocalName
		{
			get
			{
				return this.strDocumentName;
			}
		}

		// Token: 0x170007DF RID: 2015
		// (get) Token: 0x06001949 RID: 6473 RVA: 0x000E9474 File Offset: 0x000E8074
		[Nullable(2)]
		public XmlElement DocumentElement
		{
			[NullableContext(2)]
			get
			{
				return (XmlElement)this.FindChild(XmlNodeType.Element);
			}
		}

		// Token: 0x170007E0 RID: 2016
		// (get) Token: 0x0600194A RID: 6474 RVA: 0x0007108E File Offset: 0x0006FC8E
		internal override bool IsContainer
		{
			get
			{
				return true;
			}
		}

		// Token: 0x170007E1 RID: 2017
		// (get) Token: 0x0600194B RID: 6475 RVA: 0x000E9482 File Offset: 0x000E8082
		// (set) Token: 0x0600194C RID: 6476 RVA: 0x000E948A File Offset: 0x000E808A
		[Nullable(2)]
		internal override XmlLinkedNode LastNode
		{
			get
			{
				return this._lastChild;
			}
			set
			{
				this._lastChild = value;
			}
		}

		// Token: 0x170007E2 RID: 2018
		// (get) Token: 0x0600194D RID: 6477 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public override XmlDocument OwnerDocument
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x170007E3 RID: 2019
		// (get) Token: 0x0600194E RID: 6478 RVA: 0x000E9493 File Offset: 0x000E8093
		// (set) Token: 0x0600194F RID: 6479 RVA: 0x000E94B4 File Offset: 0x000E80B4
		public XmlSchemaSet Schemas
		{
			get
			{
				if (this._schemas == null)
				{
					this._schemas = new XmlSchemaSet(this.NameTable);
				}
				return this._schemas;
			}
			set
			{
				this._schemas = value;
			}
		}

		// Token: 0x170007E4 RID: 2020
		// (get) Token: 0x06001950 RID: 6480 RVA: 0x000E94BD File Offset: 0x000E80BD
		internal bool CanReportValidity
		{
			get
			{
				return this._reportValidity;
			}
		}

		// Token: 0x170007E5 RID: 2021
		// (get) Token: 0x06001951 RID: 6481 RVA: 0x000E94C5 File Offset: 0x000E80C5
		internal bool HasSetResolver
		{
			get
			{
				return this.bSetResolver;
			}
		}

		// Token: 0x06001952 RID: 6482 RVA: 0x000E94CD File Offset: 0x000E80CD
		internal XmlResolver GetResolver()
		{
			return this._resolver;
		}

		// Token: 0x170007E6 RID: 2022
		// (set) Token: 0x06001953 RID: 6483 RVA: 0x000E94D8 File Offset: 0x000E80D8
		public virtual XmlResolver XmlResolver
		{
			set
			{
				this._resolver = value;
				if (!this.bSetResolver)
				{
					this.bSetResolver = true;
				}
				XmlDocumentType documentType = this.DocumentType;
				if (documentType != null)
				{
					documentType.DtdSchemaInfo = null;
				}
			}
		}

		// Token: 0x06001954 RID: 6484 RVA: 0x000E950C File Offset: 0x000E810C
		internal override bool IsValidChildType(XmlNodeType type)
		{
			if (type != XmlNodeType.Element)
			{
				switch (type)
				{
				case XmlNodeType.ProcessingInstruction:
				case XmlNodeType.Comment:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					return true;
				case XmlNodeType.DocumentType:
					if (this.DocumentType != null)
					{
						throw new InvalidOperationException(SR.Xdom_DualDocumentTypeNode);
					}
					return true;
				case XmlNodeType.XmlDeclaration:
					if (this.Declaration != null)
					{
						throw new InvalidOperationException(SR.Xdom_DualDeclarationNode);
					}
					return true;
				}
				return false;
			}
			if (this.DocumentElement != null)
			{
				throw new InvalidOperationException(SR.Xdom_DualDocumentElementNode);
			}
			return true;
		}

		// Token: 0x06001955 RID: 6485 RVA: 0x000E9598 File Offset: 0x000E8198
		private bool HasNodeTypeInPrevSiblings(XmlNodeType nt, XmlNode refNode)
		{
			if (refNode == null)
			{
				return false;
			}
			XmlNode xmlNode = null;
			if (refNode.ParentNode != null)
			{
				xmlNode = refNode.ParentNode.FirstChild;
			}
			while (xmlNode != null)
			{
				if (xmlNode.NodeType == nt)
				{
					return true;
				}
				if (xmlNode == refNode)
				{
					break;
				}
				xmlNode = xmlNode.NextSibling;
			}
			return false;
		}

		// Token: 0x06001956 RID: 6486 RVA: 0x000E95DC File Offset: 0x000E81DC
		private bool HasNodeTypeInNextSiblings(XmlNodeType nt, XmlNode refNode)
		{
			for (XmlNode xmlNode = refNode; xmlNode != null; xmlNode = xmlNode.NextSibling)
			{
				if (xmlNode.NodeType == nt)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06001957 RID: 6487 RVA: 0x000E9604 File Offset: 0x000E8204
		internal override bool CanInsertBefore(XmlNode newChild, XmlNode refChild)
		{
			if (refChild == null)
			{
				refChild = this.FirstChild;
			}
			if (refChild == null)
			{
				return true;
			}
			XmlNodeType nodeType = newChild.NodeType;
			if (nodeType <= XmlNodeType.Comment)
			{
				if (nodeType != XmlNodeType.Element)
				{
					if (nodeType - XmlNodeType.ProcessingInstruction <= 1)
					{
						return refChild.NodeType != XmlNodeType.XmlDeclaration;
					}
				}
				else if (refChild.NodeType != XmlNodeType.XmlDeclaration)
				{
					return !this.HasNodeTypeInNextSiblings(XmlNodeType.DocumentType, refChild);
				}
			}
			else if (nodeType != XmlNodeType.DocumentType)
			{
				if (nodeType == XmlNodeType.XmlDeclaration)
				{
					return refChild == this.FirstChild;
				}
			}
			else if (refChild.NodeType != XmlNodeType.XmlDeclaration)
			{
				return !this.HasNodeTypeInPrevSiblings(XmlNodeType.Element, refChild.PreviousSibling);
			}
			return false;
		}

		// Token: 0x06001958 RID: 6488 RVA: 0x000E9690 File Offset: 0x000E8290
		internal override bool CanInsertAfter(XmlNode newChild, XmlNode refChild)
		{
			if (refChild == null)
			{
				refChild = this.LastChild;
			}
			if (refChild == null)
			{
				return true;
			}
			XmlNodeType nodeType = newChild.NodeType;
			if (nodeType != XmlNodeType.Element)
			{
				switch (nodeType)
				{
				case XmlNodeType.ProcessingInstruction:
				case XmlNodeType.Comment:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					return true;
				case XmlNodeType.DocumentType:
					return !this.HasNodeTypeInPrevSiblings(XmlNodeType.Element, refChild);
				}
				return false;
			}
			return !this.HasNodeTypeInNextSiblings(XmlNodeType.DocumentType, refChild.NextSibling);
		}

		// Token: 0x06001959 RID: 6489 RVA: 0x000E9704 File Offset: 0x000E8304
		public XmlAttribute CreateAttribute(string name)
		{
			string empty = string.Empty;
			string empty2 = string.Empty;
			string empty3 = string.Empty;
			XmlNode.SplitName(name, out empty, out empty2);
			this.SetDefaultNamespace(empty, empty2, ref empty3);
			return this.CreateAttribute(empty, empty2, empty3);
		}

		// Token: 0x0600195A RID: 6490 RVA: 0x000E9740 File Offset: 0x000E8340
		internal void SetDefaultNamespace(string prefix, string localName, ref string namespaceURI)
		{
			if (prefix == this.strXmlns || (prefix.Length == 0 && localName == this.strXmlns))
			{
				namespaceURI = this.strReservedXmlns;
				return;
			}
			if (prefix == this.strXml)
			{
				namespaceURI = this.strReservedXml;
			}
		}

		// Token: 0x0600195B RID: 6491 RVA: 0x000E9790 File Offset: 0x000E8390
		public virtual XmlCDataSection CreateCDataSection([Nullable(2)] string data)
		{
			this.fCDataNodesPresent = true;
			return new XmlCDataSection(data, this);
		}

		// Token: 0x0600195C RID: 6492 RVA: 0x000E97A0 File Offset: 0x000E83A0
		public virtual XmlComment CreateComment([Nullable(2)] string data)
		{
			return new XmlComment(data, this);
		}

		// Token: 0x0600195D RID: 6493 RVA: 0x000E97A9 File Offset: 0x000E83A9
		[NullableContext(2)]
		[return: Nullable(1)]
		public virtual XmlDocumentType CreateDocumentType([Nullable(1)] string name, string publicId, string systemId, string internalSubset)
		{
			return new XmlDocumentType(name, publicId, systemId, internalSubset, this);
		}

		// Token: 0x0600195E RID: 6494 RVA: 0x000E97B6 File Offset: 0x000E83B6
		public virtual XmlDocumentFragment CreateDocumentFragment()
		{
			return new XmlDocumentFragment(this);
		}

		// Token: 0x0600195F RID: 6495 RVA: 0x000E97C0 File Offset: 0x000E83C0
		public XmlElement CreateElement(string name)
		{
			string empty = string.Empty;
			string empty2 = string.Empty;
			XmlNode.SplitName(name, out empty, out empty2);
			return this.CreateElement(empty, empty2, string.Empty);
		}

		// Token: 0x06001960 RID: 6496 RVA: 0x000E97F0 File Offset: 0x000E83F0
		internal void AddDefaultAttributes(XmlElement elem)
		{
			SchemaInfo dtdSchemaInfo = this.DtdSchemaInfo;
			SchemaElementDecl schemaElementDecl = this.GetSchemaElementDecl(elem);
			if (schemaElementDecl != null && schemaElementDecl.AttDefs != null)
			{
				foreach (KeyValuePair<XmlQualifiedName, SchemaAttDef> keyValuePair in schemaElementDecl.AttDefs)
				{
					SchemaAttDef value = keyValuePair.Value;
					if (value.Presence == SchemaDeclBase.Use.Default || value.Presence == SchemaDeclBase.Use.Fixed)
					{
						string name = value.Name.Name;
						string attrNamespaceURI = string.Empty;
						string attrPrefix;
						if (dtdSchemaInfo.SchemaType == SchemaType.DTD)
						{
							attrPrefix = value.Name.Namespace;
						}
						else
						{
							attrPrefix = value.Prefix;
							attrNamespaceURI = value.Name.Namespace;
						}
						XmlAttribute attributeNode = this.PrepareDefaultAttribute(value, attrPrefix, name, attrNamespaceURI);
						elem.SetAttributeNode(attributeNode);
					}
				}
			}
		}

		// Token: 0x06001961 RID: 6497 RVA: 0x000E98DC File Offset: 0x000E84DC
		private SchemaElementDecl GetSchemaElementDecl(XmlElement elem)
		{
			SchemaInfo dtdSchemaInfo = this.DtdSchemaInfo;
			if (dtdSchemaInfo != null)
			{
				XmlQualifiedName key = new XmlQualifiedName(elem.LocalName, (dtdSchemaInfo.SchemaType == SchemaType.DTD) ? elem.Prefix : elem.NamespaceURI);
				SchemaElementDecl result;
				if (dtdSchemaInfo.ElementDecls.TryGetValue(key, out result))
				{
					return result;
				}
			}
			return null;
		}

		// Token: 0x06001962 RID: 6498 RVA: 0x000E992C File Offset: 0x000E852C
		private XmlAttribute PrepareDefaultAttribute(SchemaAttDef attdef, string attrPrefix, string attrLocalname, string attrNamespaceURI)
		{
			this.SetDefaultNamespace(attrPrefix, attrLocalname, ref attrNamespaceURI);
			XmlAttribute xmlAttribute = this.CreateDefaultAttribute(attrPrefix, attrLocalname, attrNamespaceURI);
			xmlAttribute.InnerXml = attdef.DefaultValueRaw;
			XmlUnspecifiedAttribute xmlUnspecifiedAttribute = xmlAttribute as XmlUnspecifiedAttribute;
			if (xmlUnspecifiedAttribute != null)
			{
				xmlUnspecifiedAttribute.SetSpecified(false);
			}
			return xmlAttribute;
		}

		// Token: 0x06001963 RID: 6499 RVA: 0x000E996C File Offset: 0x000E856C
		public virtual XmlEntityReference CreateEntityReference(string name)
		{
			return new XmlEntityReference(name, this);
		}

		// Token: 0x06001964 RID: 6500 RVA: 0x000E9975 File Offset: 0x000E8575
		public virtual XmlProcessingInstruction CreateProcessingInstruction(string target, string data)
		{
			return new XmlProcessingInstruction(target, data, this);
		}

		// Token: 0x06001965 RID: 6501 RVA: 0x000E997F File Offset: 0x000E857F
		public virtual XmlDeclaration CreateXmlDeclaration(string version, [Nullable(2)] string encoding, [Nullable(2)] string standalone)
		{
			return new XmlDeclaration(version, encoding, standalone, this);
		}

		// Token: 0x06001966 RID: 6502 RVA: 0x000E998A File Offset: 0x000E858A
		public virtual XmlText CreateTextNode([Nullable(2)] string text)
		{
			return new XmlText(text, this);
		}

		// Token: 0x06001967 RID: 6503 RVA: 0x000E9993 File Offset: 0x000E8593
		public virtual XmlSignificantWhitespace CreateSignificantWhitespace([Nullable(2)] string text)
		{
			return new XmlSignificantWhitespace(text, this);
		}

		// Token: 0x06001968 RID: 6504 RVA: 0x000E999C File Offset: 0x000E859C
		[NullableContext(2)]
		public override XPathNavigator CreateNavigator()
		{
			return this.CreateNavigator(this);
		}

		// Token: 0x06001969 RID: 6505 RVA: 0x000E99A8 File Offset: 0x000E85A8
		[return: Nullable(2)]
		protected internal virtual XPathNavigator CreateNavigator(XmlNode node)
		{
			switch (node.NodeType)
			{
			case XmlNodeType.Text:
			case XmlNodeType.CDATA:
			case XmlNodeType.SignificantWhitespace:
			{
				XmlNode parentNode = node.ParentNode;
				if (parentNode != null)
				{
					for (;;)
					{
						XmlNodeType nodeType = parentNode.NodeType;
						if (nodeType == XmlNodeType.Attribute)
						{
							break;
						}
						if (nodeType != XmlNodeType.EntityReference)
						{
							goto IL_74;
						}
						parentNode = parentNode.ParentNode;
						if (parentNode == null)
						{
							goto IL_74;
						}
					}
					return null;
				}
				IL_74:
				node = this.NormalizeText(node);
				break;
			}
			case XmlNodeType.EntityReference:
			case XmlNodeType.Entity:
			case XmlNodeType.DocumentType:
			case XmlNodeType.Notation:
			case XmlNodeType.XmlDeclaration:
				return null;
			case XmlNodeType.Whitespace:
			{
				XmlNode parentNode = node.ParentNode;
				if (parentNode != null)
				{
					for (;;)
					{
						XmlNodeType nodeType = parentNode.NodeType;
						if (nodeType == XmlNodeType.Document || nodeType == XmlNodeType.Attribute)
						{
							break;
						}
						if (nodeType != XmlNodeType.EntityReference)
						{
							goto IL_A9;
						}
						parentNode = parentNode.ParentNode;
						if (parentNode == null)
						{
							goto IL_A9;
						}
					}
					return null;
				}
				IL_A9:
				node = this.NormalizeText(node);
				break;
			}
			}
			return new DocumentXPathNavigator(this, node);
		}

		// Token: 0x0600196A RID: 6506 RVA: 0x000E9A6E File Offset: 0x000E866E
		internal static bool IsTextNode(XmlNodeType nt)
		{
			return nt - XmlNodeType.Text <= 1 || nt - XmlNodeType.Whitespace <= 1;
		}

		// Token: 0x0600196B RID: 6507 RVA: 0x000E9A80 File Offset: 0x000E8680
		private XmlNode NormalizeText(XmlNode node)
		{
			XmlNode xmlNode = null;
			XmlNode xmlNode2 = node;
			while (XmlDocument.IsTextNode(xmlNode2.NodeType))
			{
				xmlNode = xmlNode2;
				xmlNode2 = xmlNode2.PreviousSibling;
				if (xmlNode2 == null)
				{
					XmlNode xmlNode3 = xmlNode;
					while (xmlNode3.ParentNode != null && xmlNode3.ParentNode.NodeType == XmlNodeType.EntityReference)
					{
						if (xmlNode3.ParentNode.PreviousSibling != null)
						{
							xmlNode2 = xmlNode3.ParentNode.PreviousSibling;
							break;
						}
						xmlNode3 = xmlNode3.ParentNode;
						if (xmlNode3 == null)
						{
							break;
						}
					}
				}
				if (xmlNode2 == null)
				{
					break;
				}
				while (xmlNode2.NodeType == XmlNodeType.EntityReference)
				{
					xmlNode2 = xmlNode2.LastChild;
				}
			}
			return xmlNode;
		}

		// Token: 0x0600196C RID: 6508 RVA: 0x000E9AFF File Offset: 0x000E86FF
		public virtual XmlWhitespace CreateWhitespace([Nullable(2)] string text)
		{
			return new XmlWhitespace(text, this);
		}

		// Token: 0x0600196D RID: 6509 RVA: 0x000E9B08 File Offset: 0x000E8708
		public virtual XmlNodeList GetElementsByTagName(string name)
		{
			return new XmlElementList(this, name);
		}

		// Token: 0x0600196E RID: 6510 RVA: 0x000E9B14 File Offset: 0x000E8714
		public XmlAttribute CreateAttribute(string qualifiedName, [Nullable(2)] string namespaceURI)
		{
			string empty = string.Empty;
			string empty2 = string.Empty;
			XmlNode.SplitName(qualifiedName, out empty, out empty2);
			return this.CreateAttribute(empty, empty2, namespaceURI);
		}

		// Token: 0x0600196F RID: 6511 RVA: 0x000E9B40 File Offset: 0x000E8740
		public XmlElement CreateElement(string qualifiedName, [Nullable(2)] string namespaceURI)
		{
			string empty = string.Empty;
			string empty2 = string.Empty;
			XmlNode.SplitName(qualifiedName, out empty, out empty2);
			return this.CreateElement(empty, empty2, namespaceURI);
		}

		// Token: 0x06001970 RID: 6512 RVA: 0x000E9B6C File Offset: 0x000E876C
		public virtual XmlNodeList GetElementsByTagName(string localName, string namespaceURI)
		{
			return new XmlElementList(this, localName, namespaceURI);
		}

		// Token: 0x06001971 RID: 6513 RVA: 0x000E9B78 File Offset: 0x000E8778
		[return: Nullable(2)]
		public virtual XmlElement GetElementById(string elementId)
		{
			if (this._htElementIdMap != null)
			{
				ArrayList arrayList = (ArrayList)this._htElementIdMap[elementId];
				if (arrayList != null)
				{
					foreach (object obj in arrayList)
					{
						WeakReference weakReference = (WeakReference)obj;
						XmlElement xmlElement = (XmlElement)weakReference.Target;
						if (xmlElement != null && xmlElement.IsConnected())
						{
							return xmlElement;
						}
					}
				}
			}
			return null;
		}

		// Token: 0x06001972 RID: 6514 RVA: 0x000E9C08 File Offset: 0x000E8808
		public virtual XmlNode ImportNode(XmlNode node, bool deep)
		{
			return this.ImportNodeInternal(node, deep);
		}

		// Token: 0x06001973 RID: 6515 RVA: 0x000E9C14 File Offset: 0x000E8814
		private XmlNode ImportNodeInternal(XmlNode node, bool deep)
		{
			if (node == null)
			{
				throw new InvalidOperationException(SR.Xdom_Import_NullNode);
			}
			switch (node.NodeType)
			{
			case XmlNodeType.Element:
			{
				XmlNode xmlNode = this.CreateElement(node.Prefix, node.LocalName, node.NamespaceURI);
				this.ImportAttributes(node, xmlNode);
				if (deep)
				{
					this.ImportChildren(node, xmlNode, deep);
					return xmlNode;
				}
				return xmlNode;
			}
			case XmlNodeType.Attribute:
			{
				XmlNode xmlNode = this.CreateAttribute(node.Prefix, node.LocalName, node.NamespaceURI);
				this.ImportChildren(node, xmlNode, true);
				return xmlNode;
			}
			case XmlNodeType.Text:
				return this.CreateTextNode(node.Value);
			case XmlNodeType.CDATA:
				return this.CreateCDataSection(node.Value);
			case XmlNodeType.EntityReference:
				return this.CreateEntityReference(node.Name);
			case XmlNodeType.ProcessingInstruction:
				return this.CreateProcessingInstruction(node.Name, node.Value);
			case XmlNodeType.Comment:
				return this.CreateComment(node.Value);
			case XmlNodeType.DocumentType:
			{
				XmlDocumentType xmlDocumentType = (XmlDocumentType)node;
				return this.CreateDocumentType(xmlDocumentType.Name, xmlDocumentType.PublicId, xmlDocumentType.SystemId, xmlDocumentType.InternalSubset);
			}
			case XmlNodeType.DocumentFragment:
			{
				XmlNode xmlNode = this.CreateDocumentFragment();
				if (deep)
				{
					this.ImportChildren(node, xmlNode, deep);
					return xmlNode;
				}
				return xmlNode;
			}
			case XmlNodeType.Whitespace:
				return this.CreateWhitespace(node.Value);
			case XmlNodeType.SignificantWhitespace:
				return this.CreateSignificantWhitespace(node.Value);
			case XmlNodeType.XmlDeclaration:
			{
				XmlDeclaration xmlDeclaration = (XmlDeclaration)node;
				return this.CreateXmlDeclaration(xmlDeclaration.Version, xmlDeclaration.Encoding, xmlDeclaration.Standalone);
			}
			}
			throw new InvalidOperationException(SR.Format(CultureInfo.InvariantCulture, SR.Xdom_Import, node.NodeType));
		}

		// Token: 0x06001974 RID: 6516 RVA: 0x000E9DE4 File Offset: 0x000E89E4
		private void ImportAttributes(XmlNode fromElem, XmlNode toElem)
		{
			int count = fromElem.Attributes.Count;
			for (int i = 0; i < count; i++)
			{
				if (fromElem.Attributes[i].Specified)
				{
					toElem.Attributes.SetNamedItem(this.ImportNodeInternal(fromElem.Attributes[i], true));
				}
			}
		}

		// Token: 0x06001975 RID: 6517 RVA: 0x000E9E3C File Offset: 0x000E8A3C
		private void ImportChildren(XmlNode fromNode, XmlNode toNode, bool deep)
		{
			for (XmlNode xmlNode = fromNode.FirstChild; xmlNode != null; xmlNode = xmlNode.NextSibling)
			{
				toNode.AppendChild(this.ImportNodeInternal(xmlNode, deep));
			}
		}

		// Token: 0x170007E7 RID: 2023
		// (get) Token: 0x06001976 RID: 6518 RVA: 0x000E9E6B File Offset: 0x000E8A6B
		public XmlNameTable NameTable
		{
			get
			{
				return this._implementation.NameTable;
			}
		}

		// Token: 0x06001977 RID: 6519 RVA: 0x000E9E78 File Offset: 0x000E8A78
		public virtual XmlAttribute CreateAttribute([Nullable(2)] string prefix, string localName, [Nullable(2)] string namespaceURI)
		{
			return new XmlAttribute(this.AddAttrXmlName(prefix, localName, namespaceURI, null), this);
		}

		// Token: 0x06001978 RID: 6520 RVA: 0x000E9E8A File Offset: 0x000E8A8A
		protected internal virtual XmlAttribute CreateDefaultAttribute([Nullable(2)] string prefix, string localName, [Nullable(2)] string namespaceURI)
		{
			return new XmlUnspecifiedAttribute(prefix, localName, namespaceURI, this);
		}

		// Token: 0x06001979 RID: 6521 RVA: 0x000E9E98 File Offset: 0x000E8A98
		public virtual XmlElement CreateElement([Nullable(2)] string prefix, string localName, [Nullable(2)] string namespaceURI)
		{
			XmlElement xmlElement = new XmlElement(this.AddXmlName(prefix, localName, namespaceURI, null), true, this);
			if (!this.IsLoading)
			{
				this.AddDefaultAttributes(xmlElement);
			}
			return xmlElement;
		}

		// Token: 0x170007E8 RID: 2024
		// (get) Token: 0x0600197A RID: 6522 RVA: 0x000E9EC7 File Offset: 0x000E8AC7
		// (set) Token: 0x0600197B RID: 6523 RVA: 0x000E9ECF File Offset: 0x000E8ACF
		public bool PreserveWhitespace
		{
			get
			{
				return this._preserveWhitespace;
			}
			set
			{
				this._preserveWhitespace = value;
			}
		}

		// Token: 0x170007E9 RID: 2025
		// (get) Token: 0x0600197C RID: 6524 RVA: 0x00070238 File Offset: 0x0006EE38
		public override bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		// Token: 0x170007EA RID: 2026
		// (get) Token: 0x0600197D RID: 6525 RVA: 0x000E9ED8 File Offset: 0x000E8AD8
		// (set) Token: 0x0600197E RID: 6526 RVA: 0x000E9EF4 File Offset: 0x000E8AF4
		internal XmlNamedNodeMap Entities
		{
			get
			{
				if (this._entities == null)
				{
					this._entities = new XmlNamedNodeMap(this);
				}
				return this._entities;
			}
			set
			{
				this._entities = value;
			}
		}

		// Token: 0x170007EB RID: 2027
		// (get) Token: 0x0600197F RID: 6527 RVA: 0x000E9EFD File Offset: 0x000E8AFD
		// (set) Token: 0x06001980 RID: 6528 RVA: 0x000E9F05 File Offset: 0x000E8B05
		internal bool IsLoading
		{
			get
			{
				return this._isLoading;
			}
			set
			{
				this._isLoading = value;
			}
		}

		// Token: 0x170007EC RID: 2028
		// (get) Token: 0x06001981 RID: 6529 RVA: 0x000E9F0E File Offset: 0x000E8B0E
		internal bool ActualLoadingStatus
		{
			get
			{
				return this._actualLoadingStatus;
			}
		}

		// Token: 0x06001982 RID: 6530 RVA: 0x000E9F18 File Offset: 0x000E8B18
		public virtual XmlNode CreateNode(XmlNodeType type, [Nullable(2)] string prefix, string name, [Nullable(2)] string namespaceURI)
		{
			switch (type)
			{
			case XmlNodeType.Element:
				if (prefix != null)
				{
					return this.CreateElement(prefix, name, namespaceURI);
				}
				return this.CreateElement(name, namespaceURI);
			case XmlNodeType.Attribute:
				if (prefix != null)
				{
					return this.CreateAttribute(prefix, name, namespaceURI);
				}
				return this.CreateAttribute(name, namespaceURI);
			case XmlNodeType.Text:
				return this.CreateTextNode(string.Empty);
			case XmlNodeType.CDATA:
				return this.CreateCDataSection(string.Empty);
			case XmlNodeType.EntityReference:
				return this.CreateEntityReference(name);
			case XmlNodeType.ProcessingInstruction:
				return this.CreateProcessingInstruction(name, string.Empty);
			case XmlNodeType.Comment:
				return this.CreateComment(string.Empty);
			case XmlNodeType.Document:
				return new XmlDocument();
			case XmlNodeType.DocumentType:
				return this.CreateDocumentType(name, string.Empty, string.Empty, string.Empty);
			case XmlNodeType.DocumentFragment:
				return this.CreateDocumentFragment();
			case XmlNodeType.Whitespace:
				return this.CreateWhitespace(string.Empty);
			case XmlNodeType.SignificantWhitespace:
				return this.CreateSignificantWhitespace(string.Empty);
			case XmlNodeType.XmlDeclaration:
				return this.CreateXmlDeclaration("1.0", null, null);
			}
			throw new ArgumentException(SR.Format(SR.Arg_CannotCreateNode, type));
		}

		// Token: 0x06001983 RID: 6531 RVA: 0x000EA03E File Offset: 0x000E8C3E
		public virtual XmlNode CreateNode(string nodeTypeString, string name, [Nullable(2)] string namespaceURI)
		{
			return this.CreateNode(this.ConvertToNodeType(nodeTypeString), name, namespaceURI);
		}

		// Token: 0x06001984 RID: 6532 RVA: 0x000EA04F File Offset: 0x000E8C4F
		public virtual XmlNode CreateNode(XmlNodeType type, string name, [Nullable(2)] string namespaceURI)
		{
			return this.CreateNode(type, null, name, namespaceURI);
		}

		// Token: 0x06001985 RID: 6533 RVA: 0x000EA05C File Offset: 0x000E8C5C
		[return: Nullable(2)]
		public virtual XmlNode ReadNode(XmlReader reader)
		{
			XmlNode result = null;
			try
			{
				this.IsLoading = true;
				XmlLoader xmlLoader = new XmlLoader();
				result = xmlLoader.ReadCurrentNode(this, reader);
			}
			finally
			{
				this.IsLoading = false;
			}
			return result;
		}

		// Token: 0x06001986 RID: 6534 RVA: 0x000EA09C File Offset: 0x000E8C9C
		internal XmlNodeType ConvertToNodeType(string nodeTypeString)
		{
			if (nodeTypeString == "element")
			{
				return XmlNodeType.Element;
			}
			if (nodeTypeString == "attribute")
			{
				return XmlNodeType.Attribute;
			}
			if (nodeTypeString == "text")
			{
				return XmlNodeType.Text;
			}
			if (nodeTypeString == "cdatasection")
			{
				return XmlNodeType.CDATA;
			}
			if (nodeTypeString == "entityreference")
			{
				return XmlNodeType.EntityReference;
			}
			if (nodeTypeString == "entity")
			{
				return XmlNodeType.Entity;
			}
			if (nodeTypeString == "processinginstruction")
			{
				return XmlNodeType.ProcessingInstruction;
			}
			if (nodeTypeString == "comment")
			{
				return XmlNodeType.Comment;
			}
			if (nodeTypeString == "document")
			{
				return XmlNodeType.Document;
			}
			if (nodeTypeString == "documenttype")
			{
				return XmlNodeType.DocumentType;
			}
			if (nodeTypeString == "documentfragment")
			{
				return XmlNodeType.DocumentFragment;
			}
			if (nodeTypeString == "notation")
			{
				return XmlNodeType.Notation;
			}
			if (nodeTypeString == "significantwhitespace")
			{
				return XmlNodeType.SignificantWhitespace;
			}
			if (nodeTypeString == "whitespace")
			{
				return XmlNodeType.Whitespace;
			}
			throw new ArgumentException(SR.Format(SR.Xdom_Invalid_NT_String, nodeTypeString));
		}

		// Token: 0x06001987 RID: 6535 RVA: 0x000EA191 File Offset: 0x000E8D91
		private XmlTextReader SetupReader(XmlTextReader tr)
		{
			tr.XmlValidatingReaderCompatibilityMode = true;
			tr.EntityHandling = EntityHandling.ExpandCharEntities;
			if (this.HasSetResolver)
			{
				tr.XmlResolver = this.GetResolver();
			}
			return tr;
		}

		// Token: 0x06001988 RID: 6536 RVA: 0x000EA1B8 File Offset: 0x000E8DB8
		public virtual void Load(string filename)
		{
			XmlTextReader xmlTextReader = this.SetupReader(new XmlTextReader(filename, this.NameTable));
			try
			{
				this.Load(xmlTextReader);
			}
			finally
			{
				xmlTextReader.Close();
			}
		}

		// Token: 0x06001989 RID: 6537 RVA: 0x000EA1F8 File Offset: 0x000E8DF8
		public virtual void Load(Stream inStream)
		{
			XmlTextReader xmlTextReader = this.SetupReader(new XmlTextReader(inStream, this.NameTable));
			try
			{
				this.Load(xmlTextReader);
			}
			finally
			{
				xmlTextReader.Impl.Close(false);
			}
		}

		// Token: 0x0600198A RID: 6538 RVA: 0x000EA240 File Offset: 0x000E8E40
		public virtual void Load(TextReader txtReader)
		{
			XmlTextReader xmlTextReader = this.SetupReader(new XmlTextReader(txtReader, this.NameTable));
			try
			{
				this.Load(xmlTextReader);
			}
			finally
			{
				xmlTextReader.Impl.Close(false);
			}
		}

		// Token: 0x0600198B RID: 6539 RVA: 0x000EA288 File Offset: 0x000E8E88
		public virtual void Load(XmlReader reader)
		{
			try
			{
				this.IsLoading = true;
				this._actualLoadingStatus = true;
				this.RemoveAll();
				this.fEntRefNodesPresent = false;
				this.fCDataNodesPresent = false;
				this._reportValidity = true;
				XmlLoader xmlLoader = new XmlLoader();
				xmlLoader.Load(this, reader, this._preserveWhitespace);
			}
			finally
			{
				this.IsLoading = false;
				this._actualLoadingStatus = false;
				this._reportValidity = true;
			}
		}

		// Token: 0x0600198C RID: 6540 RVA: 0x000EA2FC File Offset: 0x000E8EFC
		public virtual void LoadXml(string xml)
		{
			XmlTextReader xmlTextReader = this.SetupReader(new XmlTextReader(new StringReader(xml), this.NameTable));
			try
			{
				this.Load(xmlTextReader);
			}
			finally
			{
				xmlTextReader.Close();
			}
		}

		// Token: 0x170007ED RID: 2029
		// (get) Token: 0x0600198D RID: 6541 RVA: 0x000EA344 File Offset: 0x000E8F44
		[Nullable(2)]
		internal Encoding TextEncoding
		{
			get
			{
				if (this.Declaration != null)
				{
					string encoding = this.Declaration.Encoding;
					if (encoding.Length > 0)
					{
						return System.Text.Encoding.GetEncoding(encoding);
					}
				}
				return null;
			}
		}

		// Token: 0x170007EE RID: 2030
		// (set) Token: 0x0600198E RID: 6542 RVA: 0x000EA376 File Offset: 0x000E8F76
		public override string InnerText
		{
			[param: AllowNull]
			set
			{
				throw new InvalidOperationException(SR.Xdom_Document_Innertext);
			}
		}

		// Token: 0x170007EF RID: 2031
		// (get) Token: 0x0600198F RID: 6543 RVA: 0x000EA382 File Offset: 0x000E8F82
		// (set) Token: 0x06001990 RID: 6544 RVA: 0x000EA38A File Offset: 0x000E8F8A
		public override string InnerXml
		{
			get
			{
				return base.InnerXml;
			}
			set
			{
				this.LoadXml(value);
			}
		}

		// Token: 0x06001991 RID: 6545 RVA: 0x000EA394 File Offset: 0x000E8F94
		public virtual void Save(string filename)
		{
			if (this.DocumentElement == null)
			{
				throw new XmlException(SR.Xml_InvalidXmlDocument, SR.Xdom_NoRootEle);
			}
			XmlDOMTextWriter xmlDOMTextWriter = new XmlDOMTextWriter(filename, this.TextEncoding);
			try
			{
				if (!this._preserveWhitespace)
				{
					xmlDOMTextWriter.Formatting = Formatting.Indented;
				}
				this.WriteTo(xmlDOMTextWriter);
				xmlDOMTextWriter.Flush();
			}
			finally
			{
				xmlDOMTextWriter.Close();
			}
		}

		// Token: 0x06001992 RID: 6546 RVA: 0x000EA3FC File Offset: 0x000E8FFC
		public virtual void Save(Stream outStream)
		{
			XmlDOMTextWriter xmlDOMTextWriter = new XmlDOMTextWriter(outStream, this.TextEncoding);
			if (!this._preserveWhitespace)
			{
				xmlDOMTextWriter.Formatting = Formatting.Indented;
			}
			this.WriteTo(xmlDOMTextWriter);
			xmlDOMTextWriter.Flush();
		}

		// Token: 0x06001993 RID: 6547 RVA: 0x000EA434 File Offset: 0x000E9034
		public virtual void Save(TextWriter writer)
		{
			XmlDOMTextWriter xmlDOMTextWriter = new XmlDOMTextWriter(writer);
			if (!this._preserveWhitespace)
			{
				xmlDOMTextWriter.Formatting = Formatting.Indented;
			}
			this.Save(xmlDOMTextWriter);
		}

		// Token: 0x06001994 RID: 6548 RVA: 0x000EA460 File Offset: 0x000E9060
		public virtual void Save(XmlWriter w)
		{
			XmlNode xmlNode = this.FirstChild;
			if (xmlNode == null)
			{
				return;
			}
			if (w.WriteState == WriteState.Start)
			{
				if (xmlNode is XmlDeclaration)
				{
					if (this.Standalone.Length == 0)
					{
						w.WriteStartDocument();
					}
					else if (this.Standalone == "yes")
					{
						w.WriteStartDocument(true);
					}
					else if (this.Standalone == "no")
					{
						w.WriteStartDocument(false);
					}
					xmlNode = xmlNode.NextSibling;
				}
				else
				{
					w.WriteStartDocument();
				}
			}
			while (xmlNode != null)
			{
				xmlNode.WriteTo(w);
				xmlNode = xmlNode.NextSibling;
			}
			w.Flush();
		}

		// Token: 0x06001995 RID: 6549 RVA: 0x000EA4F9 File Offset: 0x000E90F9
		public override void WriteTo(XmlWriter w)
		{
			this.WriteContentTo(w);
		}

		// Token: 0x06001996 RID: 6550 RVA: 0x000EA504 File Offset: 0x000E9104
		public override void WriteContentTo(XmlWriter xw)
		{
			foreach (object obj in this)
			{
				XmlNode xmlNode = (XmlNode)obj;
				xmlNode.WriteTo(xw);
			}
		}

		// Token: 0x06001997 RID: 6551 RVA: 0x000EA558 File Offset: 0x000E9158
		[NullableContext(2)]
		public void Validate(ValidationEventHandler validationEventHandler)
		{
			this.Validate(validationEventHandler, this);
		}

		// Token: 0x06001998 RID: 6552 RVA: 0x000EA564 File Offset: 0x000E9164
		public void Validate([Nullable(2)] ValidationEventHandler validationEventHandler, XmlNode nodeToValidate)
		{
			if (this._schemas == null || this._schemas.Count == 0)
			{
				throw new InvalidOperationException(SR.XmlDocument_NoSchemaInfo);
			}
			XmlDocument document = nodeToValidate.Document;
			if (document != this)
			{
				throw new ArgumentException(SR.Format(SR.XmlDocument_NodeNotFromDocument, "nodeToValidate"));
			}
			if (nodeToValidate == this)
			{
				this._reportValidity = false;
			}
			DocumentSchemaValidator documentSchemaValidator = new DocumentSchemaValidator(this, this._schemas, validationEventHandler);
			documentSchemaValidator.Validate(nodeToValidate);
			if (nodeToValidate == this)
			{
				this._reportValidity = true;
			}
		}

		// Token: 0x14000004 RID: 4
		// (add) Token: 0x06001999 RID: 6553 RVA: 0x000EA5DD File Offset: 0x000E91DD
		// (remove) Token: 0x0600199A RID: 6554 RVA: 0x000EA5F6 File Offset: 0x000E91F6
		public event XmlNodeChangedEventHandler NodeInserting
		{
			add
			{
				this._onNodeInsertingDelegate = (XmlNodeChangedEventHandler)Delegate.Combine(this._onNodeInsertingDelegate, value);
			}
			remove
			{
				this._onNodeInsertingDelegate = (XmlNodeChangedEventHandler)Delegate.Remove(this._onNodeInsertingDelegate, value);
			}
		}

		// Token: 0x14000005 RID: 5
		// (add) Token: 0x0600199B RID: 6555 RVA: 0x000EA60F File Offset: 0x000E920F
		// (remove) Token: 0x0600199C RID: 6556 RVA: 0x000EA628 File Offset: 0x000E9228
		public event XmlNodeChangedEventHandler NodeInserted
		{
			add
			{
				this._onNodeInsertedDelegate = (XmlNodeChangedEventHandler)Delegate.Combine(this._onNodeInsertedDelegate, value);
			}
			remove
			{
				this._onNodeInsertedDelegate = (XmlNodeChangedEventHandler)Delegate.Remove(this._onNodeInsertedDelegate, value);
			}
		}

		// Token: 0x14000006 RID: 6
		// (add) Token: 0x0600199D RID: 6557 RVA: 0x000EA641 File Offset: 0x000E9241
		// (remove) Token: 0x0600199E RID: 6558 RVA: 0x000EA65A File Offset: 0x000E925A
		public event XmlNodeChangedEventHandler NodeRemoving
		{
			add
			{
				this._onNodeRemovingDelegate = (XmlNodeChangedEventHandler)Delegate.Combine(this._onNodeRemovingDelegate, value);
			}
			remove
			{
				this._onNodeRemovingDelegate = (XmlNodeChangedEventHandler)Delegate.Remove(this._onNodeRemovingDelegate, value);
			}
		}

		// Token: 0x14000007 RID: 7
		// (add) Token: 0x0600199F RID: 6559 RVA: 0x000EA673 File Offset: 0x000E9273
		// (remove) Token: 0x060019A0 RID: 6560 RVA: 0x000EA68C File Offset: 0x000E928C
		public event XmlNodeChangedEventHandler NodeRemoved
		{
			add
			{
				this._onNodeRemovedDelegate = (XmlNodeChangedEventHandler)Delegate.Combine(this._onNodeRemovedDelegate, value);
			}
			remove
			{
				this._onNodeRemovedDelegate = (XmlNodeChangedEventHandler)Delegate.Remove(this._onNodeRemovedDelegate, value);
			}
		}

		// Token: 0x14000008 RID: 8
		// (add) Token: 0x060019A1 RID: 6561 RVA: 0x000EA6A5 File Offset: 0x000E92A5
		// (remove) Token: 0x060019A2 RID: 6562 RVA: 0x000EA6BE File Offset: 0x000E92BE
		public event XmlNodeChangedEventHandler NodeChanging
		{
			add
			{
				this._onNodeChangingDelegate = (XmlNodeChangedEventHandler)Delegate.Combine(this._onNodeChangingDelegate, value);
			}
			remove
			{
				this._onNodeChangingDelegate = (XmlNodeChangedEventHandler)Delegate.Remove(this._onNodeChangingDelegate, value);
			}
		}

		// Token: 0x14000009 RID: 9
		// (add) Token: 0x060019A3 RID: 6563 RVA: 0x000EA6D7 File Offset: 0x000E92D7
		// (remove) Token: 0x060019A4 RID: 6564 RVA: 0x000EA6F0 File Offset: 0x000E92F0
		public event XmlNodeChangedEventHandler NodeChanged
		{
			add
			{
				this._onNodeChangedDelegate = (XmlNodeChangedEventHandler)Delegate.Combine(this._onNodeChangedDelegate, value);
			}
			remove
			{
				this._onNodeChangedDelegate = (XmlNodeChangedEventHandler)Delegate.Remove(this._onNodeChangedDelegate, value);
			}
		}

		// Token: 0x060019A5 RID: 6565 RVA: 0x000EA70C File Offset: 0x000E930C
		internal override XmlNodeChangedEventArgs GetEventArgs(XmlNode node, XmlNode oldParent, XmlNode newParent, string oldValue, string newValue, XmlNodeChangedAction action)
		{
			this._reportValidity = false;
			switch (action)
			{
			case XmlNodeChangedAction.Insert:
				if (this._onNodeInsertingDelegate == null && this._onNodeInsertedDelegate == null)
				{
					return null;
				}
				break;
			case XmlNodeChangedAction.Remove:
				if (this._onNodeRemovingDelegate == null && this._onNodeRemovedDelegate == null)
				{
					return null;
				}
				break;
			case XmlNodeChangedAction.Change:
				if (this._onNodeChangingDelegate == null && this._onNodeChangedDelegate == null)
				{
					return null;
				}
				break;
			}
			return new XmlNodeChangedEventArgs(node, oldParent, newParent, oldValue, newValue, action);
		}

		// Token: 0x060019A6 RID: 6566 RVA: 0x000EA77C File Offset: 0x000E937C
		internal XmlNodeChangedEventArgs GetInsertEventArgsForLoad(XmlNode node, XmlNode newParent)
		{
			if (this._onNodeInsertingDelegate == null && this._onNodeInsertedDelegate == null)
			{
				return null;
			}
			string value = node.Value;
			return new XmlNodeChangedEventArgs(node, null, newParent, value, value, XmlNodeChangedAction.Insert);
		}

		// Token: 0x060019A7 RID: 6567 RVA: 0x000EA7B0 File Offset: 0x000E93B0
		internal override void BeforeEvent(XmlNodeChangedEventArgs args)
		{
			if (args != null)
			{
				switch (args.Action)
				{
				case XmlNodeChangedAction.Insert:
					if (this._onNodeInsertingDelegate != null)
					{
						this._onNodeInsertingDelegate(this, args);
						return;
					}
					break;
				case XmlNodeChangedAction.Remove:
					if (this._onNodeRemovingDelegate != null)
					{
						this._onNodeRemovingDelegate(this, args);
						return;
					}
					break;
				case XmlNodeChangedAction.Change:
					if (this._onNodeChangingDelegate != null)
					{
						this._onNodeChangingDelegate(this, args);
					}
					break;
				default:
					return;
				}
			}
		}

		// Token: 0x060019A8 RID: 6568 RVA: 0x000EA81C File Offset: 0x000E941C
		internal override void AfterEvent(XmlNodeChangedEventArgs args)
		{
			if (args != null)
			{
				switch (args.Action)
				{
				case XmlNodeChangedAction.Insert:
					if (this._onNodeInsertedDelegate != null)
					{
						this._onNodeInsertedDelegate(this, args);
						return;
					}
					break;
				case XmlNodeChangedAction.Remove:
					if (this._onNodeRemovedDelegate != null)
					{
						this._onNodeRemovedDelegate(this, args);
						return;
					}
					break;
				case XmlNodeChangedAction.Change:
					if (this._onNodeChangedDelegate != null)
					{
						this._onNodeChangedDelegate(this, args);
					}
					break;
				default:
					return;
				}
			}
		}

		// Token: 0x060019A9 RID: 6569 RVA: 0x000EA888 File Offset: 0x000E9488
		internal XmlAttribute GetDefaultAttribute(XmlElement elem, string attrPrefix, string attrLocalname, string attrNamespaceURI)
		{
			SchemaInfo dtdSchemaInfo = this.DtdSchemaInfo;
			SchemaElementDecl schemaElementDecl = this.GetSchemaElementDecl(elem);
			if (schemaElementDecl != null && schemaElementDecl.AttDefs != null)
			{
				foreach (KeyValuePair<XmlQualifiedName, SchemaAttDef> keyValuePair in schemaElementDecl.AttDefs)
				{
					SchemaAttDef value = keyValuePair.Value;
					if ((value.Presence == SchemaDeclBase.Use.Default || value.Presence == SchemaDeclBase.Use.Fixed) && value.Name.Name == attrLocalname && ((dtdSchemaInfo.SchemaType == SchemaType.DTD && value.Name.Namespace == attrPrefix) || (dtdSchemaInfo.SchemaType != SchemaType.DTD && value.Name.Namespace == attrNamespaceURI)))
					{
						return this.PrepareDefaultAttribute(value, attrPrefix, attrLocalname, attrNamespaceURI);
					}
				}
			}
			return null;
		}

		// Token: 0x170007F0 RID: 2032
		// (get) Token: 0x060019AA RID: 6570 RVA: 0x000EA980 File Offset: 0x000E9580
		[Nullable(2)]
		internal string Version
		{
			get
			{
				XmlDeclaration declaration = this.Declaration;
				if (declaration != null)
				{
					return declaration.Version;
				}
				return null;
			}
		}

		// Token: 0x170007F1 RID: 2033
		// (get) Token: 0x060019AB RID: 6571 RVA: 0x000EA9A0 File Offset: 0x000E95A0
		[Nullable(2)]
		internal string Encoding
		{
			get
			{
				XmlDeclaration declaration = this.Declaration;
				if (declaration != null)
				{
					return declaration.Encoding;
				}
				return null;
			}
		}

		// Token: 0x170007F2 RID: 2034
		// (get) Token: 0x060019AC RID: 6572 RVA: 0x000EA9C0 File Offset: 0x000E95C0
		[Nullable(2)]
		internal string Standalone
		{
			get
			{
				XmlDeclaration declaration = this.Declaration;
				if (declaration != null)
				{
					return declaration.Standalone;
				}
				return null;
			}
		}

		// Token: 0x060019AD RID: 6573 RVA: 0x000EA9E0 File Offset: 0x000E95E0
		internal XmlEntity GetEntityNode(string name)
		{
			if (this.DocumentType != null)
			{
				XmlNamedNodeMap entities = this.DocumentType.Entities;
				if (entities != null)
				{
					return (XmlEntity)entities.GetNamedItem(name);
				}
			}
			return null;
		}

		// Token: 0x170007F3 RID: 2035
		// (get) Token: 0x060019AE RID: 6574 RVA: 0x000EAA14 File Offset: 0x000E9614
		public override IXmlSchemaInfo SchemaInfo
		{
			get
			{
				if (this._reportValidity)
				{
					XmlElement documentElement = this.DocumentElement;
					if (documentElement != null)
					{
						XmlSchemaValidity validity = documentElement.SchemaInfo.Validity;
						if (validity == XmlSchemaValidity.Valid)
						{
							return XmlDocument.ValidSchemaInfo;
						}
						if (validity == XmlSchemaValidity.Invalid)
						{
							return XmlDocument.InvalidSchemaInfo;
						}
					}
				}
				return XmlDocument.NotKnownSchemaInfo;
			}
		}

		// Token: 0x170007F4 RID: 2036
		// (get) Token: 0x060019AF RID: 6575 RVA: 0x000EAA5A File Offset: 0x000E965A
		public override string BaseURI
		{
			get
			{
				return this.baseURI;
			}
		}

		// Token: 0x060019B0 RID: 6576 RVA: 0x000EAA62 File Offset: 0x000E9662
		internal void SetBaseURI(string inBaseURI)
		{
			this.baseURI = inBaseURI;
		}

		// Token: 0x060019B1 RID: 6577 RVA: 0x000EAA6C File Offset: 0x000E966C
		internal override XmlNode AppendChildForLoad(XmlNode newChild, XmlDocument doc)
		{
			if (!this.IsValidChildType(newChild.NodeType))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_TypeConflict);
			}
			if (!this.CanInsertAfter(newChild, this.LastChild))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Location);
			}
			XmlNodeChangedEventArgs insertEventArgsForLoad = this.GetInsertEventArgsForLoad(newChild, this);
			if (insertEventArgsForLoad != null)
			{
				this.BeforeEvent(insertEventArgsForLoad);
			}
			XmlLinkedNode xmlLinkedNode = (XmlLinkedNode)newChild;
			if (this._lastChild == null)
			{
				xmlLinkedNode.next = xmlLinkedNode;
			}
			else
			{
				xmlLinkedNode.next = this._lastChild.next;
				this._lastChild.next = xmlLinkedNode;
			}
			this._lastChild = xmlLinkedNode;
			xmlLinkedNode.SetParentForLoad(this);
			if (insertEventArgsForLoad != null)
			{
				this.AfterEvent(insertEventArgsForLoad);
			}
			return xmlLinkedNode;
		}

		// Token: 0x170007F5 RID: 2037
		// (get) Token: 0x060019B2 RID: 6578 RVA: 0x00070238 File Offset: 0x0006EE38
		internal override XPathNodeType XPNodeType
		{
			get
			{
				return XPathNodeType.Root;
			}
		}

		// Token: 0x170007F6 RID: 2038
		// (get) Token: 0x060019B3 RID: 6579 RVA: 0x000EAB0D File Offset: 0x000E970D
		internal bool HasEntityReferences
		{
			get
			{
				return this.fEntRefNodesPresent;
			}
		}

		// Token: 0x170007F7 RID: 2039
		// (get) Token: 0x060019B4 RID: 6580 RVA: 0x000EAB18 File Offset: 0x000E9718
		internal XmlAttribute NamespaceXml
		{
			get
			{
				if (this._namespaceXml == null)
				{
					this._namespaceXml = new XmlAttribute(this.AddAttrXmlName(this.strXmlns, this.strXml, this.strReservedXmlns, null), this);
					this._namespaceXml.Value = this.strReservedXml;
				}
				return this._namespaceXml;
			}
		}

		// Token: 0x04000D46 RID: 3398
		[TupleElementNames(new string[]
		{
			"key",
			"hash"
		})]
		private static readonly ValueTuple<string, int>[] s_nameTableSeeds = new ValueTuple<string, int>[]
		{
			new ValueTuple<string, int>("#document", System.Xml.NameTable.ComputeHash32("#document")),
			new ValueTuple<string, int>("#document-fragment", System.Xml.NameTable.ComputeHash32("#document-fragment")),
			new ValueTuple<string, int>("#comment", System.Xml.NameTable.ComputeHash32("#comment")),
			new ValueTuple<string, int>("#text", System.Xml.NameTable.ComputeHash32("#text")),
			new ValueTuple<string, int>("#cdata-section", System.Xml.NameTable.ComputeHash32("#cdata-section")),
			new ValueTuple<string, int>("#entity", System.Xml.NameTable.ComputeHash32("#entity")),
			new ValueTuple<string, int>("id", System.Xml.NameTable.ComputeHash32("id")),
			new ValueTuple<string, int>("xmlns", System.Xml.NameTable.ComputeHash32("xmlns")),
			new ValueTuple<string, int>("xml", System.Xml.NameTable.ComputeHash32("xml")),
			new ValueTuple<string, int>("space", System.Xml.NameTable.ComputeHash32("space")),
			new ValueTuple<string, int>("lang", System.Xml.NameTable.ComputeHash32("lang")),
			new ValueTuple<string, int>("#whitespace", System.Xml.NameTable.ComputeHash32("#whitespace")),
			new ValueTuple<string, int>("#significant-whitespace", System.Xml.NameTable.ComputeHash32("#significant-whitespace")),
			new ValueTuple<string, int>("http://www.w3.org/2000/xmlns/", System.Xml.NameTable.ComputeHash32("http://www.w3.org/2000/xmlns/")),
			new ValueTuple<string, int>("http://www.w3.org/XML/1998/namespace", System.Xml.NameTable.ComputeHash32("http://www.w3.org/XML/1998/namespace"))
		};

		// Token: 0x04000D47 RID: 3399
		private readonly XmlImplementation _implementation;

		// Token: 0x04000D48 RID: 3400
		private readonly DomNameTable _domNameTable;

		// Token: 0x04000D49 RID: 3401
		private XmlLinkedNode _lastChild;

		// Token: 0x04000D4A RID: 3402
		private XmlNamedNodeMap _entities;

		// Token: 0x04000D4B RID: 3403
		private Hashtable _htElementIdMap;

		// Token: 0x04000D4C RID: 3404
		private Hashtable _htElementIDAttrDecl;

		// Token: 0x04000D4D RID: 3405
		private SchemaInfo _schemaInfo;

		// Token: 0x04000D4E RID: 3406
		private XmlSchemaSet _schemas;

		// Token: 0x04000D4F RID: 3407
		private bool _reportValidity;

		// Token: 0x04000D50 RID: 3408
		private bool _actualLoadingStatus;

		// Token: 0x04000D51 RID: 3409
		private XmlNodeChangedEventHandler _onNodeInsertingDelegate;

		// Token: 0x04000D52 RID: 3410
		private XmlNodeChangedEventHandler _onNodeInsertedDelegate;

		// Token: 0x04000D53 RID: 3411
		private XmlNodeChangedEventHandler _onNodeRemovingDelegate;

		// Token: 0x04000D54 RID: 3412
		private XmlNodeChangedEventHandler _onNodeRemovedDelegate;

		// Token: 0x04000D55 RID: 3413
		private XmlNodeChangedEventHandler _onNodeChangingDelegate;

		// Token: 0x04000D56 RID: 3414
		private XmlNodeChangedEventHandler _onNodeChangedDelegate;

		// Token: 0x04000D57 RID: 3415
		internal bool fEntRefNodesPresent;

		// Token: 0x04000D58 RID: 3416
		internal bool fCDataNodesPresent;

		// Token: 0x04000D59 RID: 3417
		private bool _preserveWhitespace;

		// Token: 0x04000D5A RID: 3418
		private bool _isLoading;

		// Token: 0x04000D5B RID: 3419
		internal string strDocumentName;

		// Token: 0x04000D5C RID: 3420
		internal string strDocumentFragmentName;

		// Token: 0x04000D5D RID: 3421
		internal string strCommentName;

		// Token: 0x04000D5E RID: 3422
		internal string strTextName;

		// Token: 0x04000D5F RID: 3423
		internal string strCDataSectionName;

		// Token: 0x04000D60 RID: 3424
		internal string strEntityName;

		// Token: 0x04000D61 RID: 3425
		internal string strID;

		// Token: 0x04000D62 RID: 3426
		internal string strXmlns;

		// Token: 0x04000D63 RID: 3427
		internal string strXml;

		// Token: 0x04000D64 RID: 3428
		internal string strSpace;

		// Token: 0x04000D65 RID: 3429
		internal string strLang;

		// Token: 0x04000D66 RID: 3430
		internal string strNonSignificantWhitespaceName;

		// Token: 0x04000D67 RID: 3431
		internal string strSignificantWhitespaceName;

		// Token: 0x04000D68 RID: 3432
		internal string strReservedXmlns;

		// Token: 0x04000D69 RID: 3433
		internal string strReservedXml;

		// Token: 0x04000D6A RID: 3434
		internal string baseURI;

		// Token: 0x04000D6B RID: 3435
		private XmlResolver _resolver;

		// Token: 0x04000D6C RID: 3436
		internal bool bSetResolver;

		// Token: 0x04000D6D RID: 3437
		internal object objLock;

		// Token: 0x04000D6E RID: 3438
		private XmlAttribute _namespaceXml;

		// Token: 0x04000D6F RID: 3439
		internal static EmptyEnumerator EmptyEnumerator = new EmptyEnumerator();

		// Token: 0x04000D70 RID: 3440
		internal static IXmlSchemaInfo NotKnownSchemaInfo = new XmlSchemaInfo(XmlSchemaValidity.NotKnown);

		// Token: 0x04000D71 RID: 3441
		internal static IXmlSchemaInfo ValidSchemaInfo = new XmlSchemaInfo(XmlSchemaValidity.Valid);

		// Token: 0x04000D72 RID: 3442
		internal static IXmlSchemaInfo InvalidSchemaInfo = new XmlSchemaInfo(XmlSchemaValidity.Invalid);
	}
}
