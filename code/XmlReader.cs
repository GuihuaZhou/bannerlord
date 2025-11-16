using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace System.Xml
{
	// Token: 0x02000151 RID: 337
	[Nullable(0)]
	[NullableContext(1)]
	[DebuggerDisplay("{debuggerDisplayProxy}")]
	[DebuggerDisplay("{debuggerDisplayProxy}")]
	public abstract class XmlReader : IDisposable
	{
		// Token: 0x170005EF RID: 1519
		// (get) Token: 0x06000F97 RID: 3991 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public virtual XmlReaderSettings Settings
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x170005F0 RID: 1520
		// (get) Token: 0x06000F98 RID: 3992
		public abstract XmlNodeType NodeType { get; }

		// Token: 0x170005F1 RID: 1521
		// (get) Token: 0x06000F99 RID: 3993 RVA: 0x000A815E File Offset: 0x000A6D5E
		public virtual string Name
		{
			get
			{
				if (this.Prefix.Length == 0)
				{
					return this.LocalName;
				}
				return this.NameTable.Add(this.Prefix + ":" + this.LocalName);
			}
		}

		// Token: 0x170005F2 RID: 1522
		// (get) Token: 0x06000F9A RID: 3994
		public abstract string LocalName { get; }

		// Token: 0x170005F3 RID: 1523
		// (get) Token: 0x06000F9B RID: 3995
		public abstract string NamespaceURI { get; }

		// Token: 0x170005F4 RID: 1524
		// (get) Token: 0x06000F9C RID: 3996
		public abstract string Prefix { get; }

		// Token: 0x170005F5 RID: 1525
		// (get) Token: 0x06000F9D RID: 3997 RVA: 0x000A8195 File Offset: 0x000A6D95
		public virtual bool HasValue
		{
			get
			{
				return XmlReader.HasValueInternal(this.NodeType);
			}
		}

		// Token: 0x170005F6 RID: 1526
		// (get) Token: 0x06000F9E RID: 3998
		public abstract string Value { get; }

		// Token: 0x170005F7 RID: 1527
		// (get) Token: 0x06000F9F RID: 3999
		public abstract int Depth { get; }

		// Token: 0x170005F8 RID: 1528
		// (get) Token: 0x06000FA0 RID: 4000
		[Nullable(2)]
		public abstract string BaseURI { [NullableContext(2)] get; }

		// Token: 0x170005F9 RID: 1529
		// (get) Token: 0x06000FA1 RID: 4001
		public abstract bool IsEmptyElement { get; }

		// Token: 0x170005FA RID: 1530
		// (get) Token: 0x06000FA2 RID: 4002 RVA: 0x00070238 File Offset: 0x0006EE38
		public virtual bool IsDefault
		{
			get
			{
				return false;
			}
		}

		// Token: 0x170005FB RID: 1531
		// (get) Token: 0x06000FA3 RID: 4003 RVA: 0x000A81A2 File Offset: 0x000A6DA2
		public virtual char QuoteChar
		{
			get
			{
				return '"';
			}
		}

		// Token: 0x170005FC RID: 1532
		// (get) Token: 0x06000FA4 RID: 4004 RVA: 0x00070238 File Offset: 0x0006EE38
		public virtual XmlSpace XmlSpace
		{
			get
			{
				return XmlSpace.None;
			}
		}

		// Token: 0x170005FD RID: 1533
		// (get) Token: 0x06000FA5 RID: 4005 RVA: 0x00070E99 File Offset: 0x0006FA99
		public virtual string XmlLang
		{
			get
			{
				return string.Empty;
			}
		}

		// Token: 0x170005FE RID: 1534
		// (get) Token: 0x06000FA6 RID: 4006 RVA: 0x000A81A6 File Offset: 0x000A6DA6
		[Nullable(2)]
		public virtual IXmlSchemaInfo SchemaInfo
		{
			[NullableContext(2)]
			get
			{
				return this as IXmlSchemaInfo;
			}
		}

		// Token: 0x170005FF RID: 1535
		// (get) Token: 0x06000FA7 RID: 4007 RVA: 0x000A81AE File Offset: 0x000A6DAE
		public virtual Type ValueType
		{
			get
			{
				return typeof(string);
			}
		}

		// Token: 0x06000FA8 RID: 4008 RVA: 0x000A81BA File Offset: 0x000A6DBA
		public virtual object ReadContentAsObject()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsObject");
			}
			return this.InternalReadContentAsString();
		}

		// Token: 0x06000FA9 RID: 4009 RVA: 0x000A81D8 File Offset: 0x000A6DD8
		public virtual bool ReadContentAsBoolean()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsBoolean");
			}
			bool result;
			try
			{
				result = XmlConvert.ToBoolean(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "Boolean", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FAA RID: 4010 RVA: 0x000A8234 File Offset: 0x000A6E34
		public virtual DateTime ReadContentAsDateTime()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsDateTime");
			}
			DateTime result;
			try
			{
				result = XmlConvert.ToDateTime(this.InternalReadContentAsString(), XmlDateTimeSerializationMode.RoundtripKind);
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "DateTime", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FAB RID: 4011 RVA: 0x000A8290 File Offset: 0x000A6E90
		public virtual DateTimeOffset ReadContentAsDateTimeOffset()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsDateTimeOffset");
			}
			DateTimeOffset result;
			try
			{
				result = XmlConvert.ToDateTimeOffset(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "DateTimeOffset", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FAC RID: 4012 RVA: 0x000A82EC File Offset: 0x000A6EEC
		public virtual double ReadContentAsDouble()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsDouble");
			}
			double result;
			try
			{
				result = XmlConvert.ToDouble(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "Double", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FAD RID: 4013 RVA: 0x000A8348 File Offset: 0x000A6F48
		public virtual float ReadContentAsFloat()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsFloat");
			}
			float result;
			try
			{
				result = XmlConvert.ToSingle(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "Float", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FAE RID: 4014 RVA: 0x000A83A4 File Offset: 0x000A6FA4
		public virtual decimal ReadContentAsDecimal()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsDecimal");
			}
			decimal result;
			try
			{
				result = XmlConvert.ToDecimal(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "Decimal", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FAF RID: 4015 RVA: 0x000A8400 File Offset: 0x000A7000
		public virtual int ReadContentAsInt()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsInt");
			}
			int result;
			try
			{
				result = XmlConvert.ToInt32(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "Int", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FB0 RID: 4016 RVA: 0x000A845C File Offset: 0x000A705C
		public virtual long ReadContentAsLong()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsLong");
			}
			long result;
			try
			{
				result = XmlConvert.ToInt64(this.InternalReadContentAsString());
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, "Long", innerException, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FB1 RID: 4017 RVA: 0x000A84B8 File Offset: 0x000A70B8
		public virtual string ReadContentAsString()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsString");
			}
			return this.InternalReadContentAsString();
		}

		// Token: 0x06000FB2 RID: 4018 RVA: 0x000A84D4 File Offset: 0x000A70D4
		public virtual object ReadContentAs(Type returnType, [Nullable(2)] IXmlNamespaceResolver namespaceResolver)
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAs");
			}
			string text = this.InternalReadContentAsString();
			if (returnType == typeof(string))
			{
				return text;
			}
			object result;
			try
			{
				result = XmlUntypedStringConverter.Instance.FromString(text, returnType, (namespaceResolver == null) ? (this as IXmlNamespaceResolver) : namespaceResolver);
			}
			catch (FormatException innerException)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, returnType.ToString(), innerException, this as IXmlLineInfo);
			}
			catch (InvalidCastException innerException2)
			{
				throw new XmlException(SR.Xml_ReadContentAsFormatException, returnType.ToString(), innerException2, this as IXmlLineInfo);
			}
			return result;
		}

		// Token: 0x06000FB3 RID: 4019 RVA: 0x000A857C File Offset: 0x000A717C
		public virtual object ReadElementContentAsObject()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsObject"))
			{
				object result = this.ReadContentAsObject();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return string.Empty;
		}

		// Token: 0x06000FB4 RID: 4020 RVA: 0x000A85AA File Offset: 0x000A71AA
		public virtual object ReadElementContentAsObject(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsObject();
		}

		// Token: 0x06000FB5 RID: 4021 RVA: 0x000A85BC File Offset: 0x000A71BC
		public virtual bool ReadElementContentAsBoolean()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsBoolean"))
			{
				bool result = this.ReadContentAsBoolean();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToBoolean(string.Empty);
		}

		// Token: 0x06000FB6 RID: 4022 RVA: 0x000A85EF File Offset: 0x000A71EF
		public virtual bool ReadElementContentAsBoolean(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsBoolean();
		}

		// Token: 0x06000FB7 RID: 4023 RVA: 0x000A8600 File Offset: 0x000A7200
		public virtual DateTime ReadElementContentAsDateTime()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsDateTime"))
			{
				DateTime result = this.ReadContentAsDateTime();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToDateTime(string.Empty, XmlDateTimeSerializationMode.RoundtripKind);
		}

		// Token: 0x06000FB8 RID: 4024 RVA: 0x000A8634 File Offset: 0x000A7234
		public virtual DateTime ReadElementContentAsDateTime(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsDateTime();
		}

		// Token: 0x06000FB9 RID: 4025 RVA: 0x000A8644 File Offset: 0x000A7244
		public virtual double ReadElementContentAsDouble()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsDouble"))
			{
				double result = this.ReadContentAsDouble();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToDouble(string.Empty);
		}

		// Token: 0x06000FBA RID: 4026 RVA: 0x000A8677 File Offset: 0x000A7277
		public virtual double ReadElementContentAsDouble(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsDouble();
		}

		// Token: 0x06000FBB RID: 4027 RVA: 0x000A8688 File Offset: 0x000A7288
		public virtual float ReadElementContentAsFloat()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsFloat"))
			{
				float result = this.ReadContentAsFloat();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToSingle(string.Empty);
		}

		// Token: 0x06000FBC RID: 4028 RVA: 0x000A86BB File Offset: 0x000A72BB
		public virtual float ReadElementContentAsFloat(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsFloat();
		}

		// Token: 0x06000FBD RID: 4029 RVA: 0x000A86CC File Offset: 0x000A72CC
		public virtual decimal ReadElementContentAsDecimal()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsDecimal"))
			{
				decimal result = this.ReadContentAsDecimal();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToDecimal(string.Empty);
		}

		// Token: 0x06000FBE RID: 4030 RVA: 0x000A86FF File Offset: 0x000A72FF
		public virtual decimal ReadElementContentAsDecimal(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsDecimal();
		}

		// Token: 0x06000FBF RID: 4031 RVA: 0x000A8710 File Offset: 0x000A7310
		public virtual int ReadElementContentAsInt()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsInt"))
			{
				int result = this.ReadContentAsInt();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToInt32(string.Empty);
		}

		// Token: 0x06000FC0 RID: 4032 RVA: 0x000A8743 File Offset: 0x000A7343
		public virtual int ReadElementContentAsInt(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsInt();
		}

		// Token: 0x06000FC1 RID: 4033 RVA: 0x000A8754 File Offset: 0x000A7354
		public virtual long ReadElementContentAsLong()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsLong"))
			{
				long result = this.ReadContentAsLong();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return XmlConvert.ToInt64(string.Empty);
		}

		// Token: 0x06000FC2 RID: 4034 RVA: 0x000A8787 File Offset: 0x000A7387
		public virtual long ReadElementContentAsLong(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsLong();
		}

		// Token: 0x06000FC3 RID: 4035 RVA: 0x000A8798 File Offset: 0x000A7398
		public virtual string ReadElementContentAsString()
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAsString"))
			{
				string result = this.ReadContentAsString();
				this.FinishReadElementContentAsXxx();
				return result;
			}
			return string.Empty;
		}

		// Token: 0x06000FC4 RID: 4036 RVA: 0x000A87C6 File Offset: 0x000A73C6
		public virtual string ReadElementContentAsString(string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAsString();
		}

		// Token: 0x06000FC5 RID: 4037 RVA: 0x000A87D8 File Offset: 0x000A73D8
		public virtual object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
		{
			if (this.SetupReadElementContentAsXxx("ReadElementContentAs"))
			{
				object result = this.ReadContentAs(returnType, namespaceResolver);
				this.FinishReadElementContentAsXxx();
				return result;
			}
			if (!(returnType == typeof(string)))
			{
				return XmlUntypedStringConverter.Instance.FromString(string.Empty, returnType, namespaceResolver);
			}
			return string.Empty;
		}

		// Token: 0x06000FC6 RID: 4038 RVA: 0x000A882C File Offset: 0x000A742C
		public virtual object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver, string localName, string namespaceURI)
		{
			this.CheckElement(localName, namespaceURI);
			return this.ReadElementContentAs(returnType, namespaceResolver);
		}

		// Token: 0x17000600 RID: 1536
		// (get) Token: 0x06000FC7 RID: 4039
		public abstract int AttributeCount { get; }

		// Token: 0x06000FC8 RID: 4040
		[return: Nullable(2)]
		public abstract string GetAttribute(string name);

		// Token: 0x06000FC9 RID: 4041
		[NullableContext(2)]
		public abstract string GetAttribute([Nullable(1)] string name, string namespaceURI);

		// Token: 0x06000FCA RID: 4042
		public abstract string GetAttribute(int i);

		// Token: 0x17000601 RID: 1537
		public virtual string this[int i]
		{
			get
			{
				return this.GetAttribute(i);
			}
		}

		// Token: 0x17000602 RID: 1538
		[Nullable(2)]
		public virtual string this[string name]
		{
			[return: Nullable(2)]
			get
			{
				return this.GetAttribute(name);
			}
		}

		// Token: 0x17000603 RID: 1539
		[Nullable(2)]
		public virtual string this[[Nullable(1)] string name, string namespaceURI]
		{
			[NullableContext(2)]
			get
			{
				return this.GetAttribute(name, namespaceURI);
			}
		}

		// Token: 0x06000FCE RID: 4046
		public abstract bool MoveToAttribute(string name);

		// Token: 0x06000FCF RID: 4047
		public abstract bool MoveToAttribute(string name, [Nullable(2)] string ns);

		// Token: 0x06000FD0 RID: 4048 RVA: 0x000A885C File Offset: 0x000A745C
		public virtual void MoveToAttribute(int i)
		{
			if (i < 0 || i >= this.AttributeCount)
			{
				throw new ArgumentOutOfRangeException("i");
			}
			this.MoveToElement();
			this.MoveToFirstAttribute();
			for (int j = 0; j < i; j++)
			{
				this.MoveToNextAttribute();
			}
		}

		// Token: 0x06000FD1 RID: 4049
		public abstract bool MoveToFirstAttribute();

		// Token: 0x06000FD2 RID: 4050
		public abstract bool MoveToNextAttribute();

		// Token: 0x06000FD3 RID: 4051
		public abstract bool MoveToElement();

		// Token: 0x06000FD4 RID: 4052
		public abstract bool ReadAttributeValue();

		// Token: 0x06000FD5 RID: 4053
		public abstract bool Read();

		// Token: 0x17000604 RID: 1540
		// (get) Token: 0x06000FD6 RID: 4054
		public abstract bool EOF { get; }

		// Token: 0x06000FD7 RID: 4055 RVA: 0x00070D8A File Offset: 0x0006F98A
		public virtual void Close()
		{
		}

		// Token: 0x17000605 RID: 1541
		// (get) Token: 0x06000FD8 RID: 4056
		public abstract ReadState ReadState { get; }

		// Token: 0x06000FD9 RID: 4057 RVA: 0x000A88A2 File Offset: 0x000A74A2
		public virtual void Skip()
		{
			if (this.ReadState != ReadState.Interactive)
			{
				return;
			}
			this.SkipSubtree();
		}

		// Token: 0x17000606 RID: 1542
		// (get) Token: 0x06000FDA RID: 4058
		public abstract XmlNameTable NameTable { get; }

		// Token: 0x06000FDB RID: 4059
		[return: Nullable(2)]
		public abstract string LookupNamespace(string prefix);

		// Token: 0x17000607 RID: 1543
		// (get) Token: 0x06000FDC RID: 4060 RVA: 0x00070238 File Offset: 0x0006EE38
		public virtual bool CanResolveEntity
		{
			get
			{
				return false;
			}
		}

		// Token: 0x06000FDD RID: 4061
		public abstract void ResolveEntity();

		// Token: 0x17000608 RID: 1544
		// (get) Token: 0x06000FDE RID: 4062 RVA: 0x00070238 File Offset: 0x0006EE38
		public virtual bool CanReadBinaryContent
		{
			get
			{
				return false;
			}
		}

		// Token: 0x06000FDF RID: 4063 RVA: 0x000A88B5 File Offset: 0x000A74B5
		public virtual int ReadContentAsBase64(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadContentAsBase64"));
		}

		// Token: 0x06000FE0 RID: 4064 RVA: 0x000A88CB File Offset: 0x000A74CB
		public virtual int ReadElementContentAsBase64(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadElementContentAsBase64"));
		}

		// Token: 0x06000FE1 RID: 4065 RVA: 0x000A88E1 File Offset: 0x000A74E1
		public virtual int ReadContentAsBinHex(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadContentAsBinHex"));
		}

		// Token: 0x06000FE2 RID: 4066 RVA: 0x000A88F7 File Offset: 0x000A74F7
		public virtual int ReadElementContentAsBinHex(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadElementContentAsBinHex"));
		}

		// Token: 0x17000609 RID: 1545
		// (get) Token: 0x06000FE3 RID: 4067 RVA: 0x00070238 File Offset: 0x0006EE38
		public virtual bool CanReadValueChunk
		{
			get
			{
				return false;
			}
		}

		// Token: 0x06000FE4 RID: 4068 RVA: 0x000A890D File Offset: 0x000A750D
		public virtual int ReadValueChunk(char[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Xml_ReadValueChunkNotSupported);
		}

		// Token: 0x06000FE5 RID: 4069 RVA: 0x000A891C File Offset: 0x000A751C
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual string ReadString()
		{
			if (this.ReadState != ReadState.Interactive)
			{
				return string.Empty;
			}
			this.MoveToElement();
			if (this.NodeType == XmlNodeType.Element)
			{
				if (this.IsEmptyElement)
				{
					return string.Empty;
				}
				if (!this.Read())
				{
					throw new InvalidOperationException(SR.Xml_InvalidOperation);
				}
				if (this.NodeType == XmlNodeType.EndElement)
				{
					return string.Empty;
				}
			}
			string text = string.Empty;
			while (XmlReader.IsTextualNode(this.NodeType))
			{
				text += this.Value;
				if (!this.Read())
				{
					break;
				}
			}
			return text;
		}

		// Token: 0x06000FE6 RID: 4070 RVA: 0x000A89A4 File Offset: 0x000A75A4
		public virtual XmlNodeType MoveToContent()
		{
			for (;;)
			{
				XmlNodeType nodeType = this.NodeType;
				switch (nodeType)
				{
				case XmlNodeType.Element:
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.EntityReference:
					goto IL_33;
				case XmlNodeType.Attribute:
					goto IL_2C;
				default:
					if (nodeType - XmlNodeType.EndElement <= 1)
					{
						goto IL_33;
					}
					if (!this.Read())
					{
						goto Block_2;
					}
					break;
				}
			}
			IL_2C:
			this.MoveToElement();
			IL_33:
			return this.NodeType;
			Block_2:
			return this.NodeType;
		}

		// Token: 0x06000FE7 RID: 4071 RVA: 0x000A89FC File Offset: 0x000A75FC
		public virtual void ReadStartElement()
		{
			if (this.MoveToContent() != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			this.Read();
		}

		// Token: 0x06000FE8 RID: 4072 RVA: 0x000A8A40 File Offset: 0x000A7640
		public virtual void ReadStartElement(string name)
		{
			if (this.MoveToContent() != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			if (this.Name == name)
			{
				this.Read();
				return;
			}
			throw new XmlException(SR.Xml_ElementNotFound, name, this as IXmlLineInfo);
		}

		// Token: 0x06000FE9 RID: 4073 RVA: 0x000A8AA4 File Offset: 0x000A76A4
		public virtual void ReadStartElement(string localname, string ns)
		{
			if (this.MoveToContent() != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			if (this.LocalName == localname && this.NamespaceURI == ns)
			{
				this.Read();
				return;
			}
			throw new XmlException(SR.Xml_ElementNotFoundNs, new string[]
			{
				localname,
				ns
			}, this as IXmlLineInfo);
		}

		// Token: 0x06000FEA RID: 4074 RVA: 0x000A8B24 File Offset: 0x000A7724
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual string ReadElementString()
		{
			string result = string.Empty;
			if (this.MoveToContent() != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			if (!this.IsEmptyElement)
			{
				this.Read();
				result = this.ReadString();
				if (this.NodeType != XmlNodeType.EndElement)
				{
					throw new XmlException(SR.Xml_UnexpectedNodeInSimpleContent, new string[]
					{
						this.NodeType.ToString(),
						"ReadElementString"
					}, this as IXmlLineInfo);
				}
				this.Read();
			}
			else
			{
				this.Read();
			}
			return result;
		}

		// Token: 0x06000FEB RID: 4075 RVA: 0x000A8BCC File Offset: 0x000A77CC
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual string ReadElementString(string name)
		{
			string result = string.Empty;
			if (this.MoveToContent() != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			if (this.Name != name)
			{
				throw new XmlException(SR.Xml_ElementNotFound, name, this as IXmlLineInfo);
			}
			if (!this.IsEmptyElement)
			{
				result = this.ReadString();
				if (this.NodeType != XmlNodeType.EndElement)
				{
					throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
				}
				this.Read();
			}
			else
			{
				this.Read();
			}
			return result;
		}

		// Token: 0x06000FEC RID: 4076 RVA: 0x000A8C7C File Offset: 0x000A787C
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual string ReadElementString(string localname, string ns)
		{
			string result = string.Empty;
			if (this.MoveToContent() != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			if (this.LocalName != localname || this.NamespaceURI != ns)
			{
				throw new XmlException(SR.Xml_ElementNotFoundNs, new string[]
				{
					localname,
					ns
				}, this as IXmlLineInfo);
			}
			if (!this.IsEmptyElement)
			{
				result = this.ReadString();
				if (this.NodeType != XmlNodeType.EndElement)
				{
					throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
				}
				this.Read();
			}
			else
			{
				this.Read();
			}
			return result;
		}

		// Token: 0x06000FED RID: 4077 RVA: 0x000A8D48 File Offset: 0x000A7948
		public virtual void ReadEndElement()
		{
			if (this.MoveToContent() != XmlNodeType.EndElement)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			this.Read();
		}

		// Token: 0x06000FEE RID: 4078 RVA: 0x000A8D8B File Offset: 0x000A798B
		public virtual bool IsStartElement()
		{
			return this.MoveToContent() == XmlNodeType.Element;
		}

		// Token: 0x06000FEF RID: 4079 RVA: 0x000A8D96 File Offset: 0x000A7996
		public virtual bool IsStartElement(string name)
		{
			return this.MoveToContent() == XmlNodeType.Element && this.Name == name;
		}

		// Token: 0x06000FF0 RID: 4080 RVA: 0x000A8DAF File Offset: 0x000A79AF
		public virtual bool IsStartElement(string localname, string ns)
		{
			return this.MoveToContent() == XmlNodeType.Element && this.LocalName == localname && this.NamespaceURI == ns;
		}

		// Token: 0x06000FF1 RID: 4081 RVA: 0x000A8DD8 File Offset: 0x000A79D8
		public virtual bool ReadToFollowing(string name)
		{
			if (name == null || name.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(name, "name");
			}
			name = this.NameTable.Add(name);
			while (this.Read())
			{
				if (this.NodeType == XmlNodeType.Element && Ref.Equal(name, this.Name))
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000FF2 RID: 4082 RVA: 0x000A8E30 File Offset: 0x000A7A30
		public virtual bool ReadToFollowing(string localName, string namespaceURI)
		{
			if (localName == null || localName.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(localName, "localName");
			}
			if (namespaceURI == null)
			{
				throw new ArgumentNullException("namespaceURI");
			}
			localName = this.NameTable.Add(localName);
			namespaceURI = this.NameTable.Add(namespaceURI);
			while (this.Read())
			{
				if (this.NodeType == XmlNodeType.Element && Ref.Equal(localName, this.LocalName) && Ref.Equal(namespaceURI, this.NamespaceURI))
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000FF3 RID: 4083 RVA: 0x000A8EB0 File Offset: 0x000A7AB0
		public virtual bool ReadToDescendant(string name)
		{
			if (name == null || name.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(name, "name");
			}
			int num = this.Depth;
			if (this.NodeType != XmlNodeType.Element)
			{
				if (this.ReadState != ReadState.Initial)
				{
					return false;
				}
				num--;
			}
			else if (this.IsEmptyElement)
			{
				return false;
			}
			name = this.NameTable.Add(name);
			while (this.Read() && this.Depth > num)
			{
				if (this.NodeType == XmlNodeType.Element && Ref.Equal(name, this.Name))
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000FF4 RID: 4084 RVA: 0x000A8F3C File Offset: 0x000A7B3C
		public virtual bool ReadToDescendant(string localName, string namespaceURI)
		{
			if (localName == null || localName.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(localName, "localName");
			}
			if (namespaceURI == null)
			{
				throw new ArgumentNullException("namespaceURI");
			}
			int num = this.Depth;
			if (this.NodeType != XmlNodeType.Element)
			{
				if (this.ReadState != ReadState.Initial)
				{
					return false;
				}
				num--;
			}
			else if (this.IsEmptyElement)
			{
				return false;
			}
			localName = this.NameTable.Add(localName);
			namespaceURI = this.NameTable.Add(namespaceURI);
			while (this.Read() && this.Depth > num)
			{
				if (this.NodeType == XmlNodeType.Element && Ref.Equal(localName, this.LocalName) && Ref.Equal(namespaceURI, this.NamespaceURI))
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000FF5 RID: 4085 RVA: 0x000A8FF0 File Offset: 0x000A7BF0
		public virtual bool ReadToNextSibling(string name)
		{
			if (name == null || name.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(name, "name");
			}
			name = this.NameTable.Add(name);
			while (this.SkipSubtree())
			{
				XmlNodeType nodeType = this.NodeType;
				if (nodeType == XmlNodeType.Element && Ref.Equal(name, this.Name))
				{
					return true;
				}
				if (nodeType == XmlNodeType.EndElement || this.EOF)
				{
					break;
				}
			}
			return false;
		}

		// Token: 0x06000FF6 RID: 4086 RVA: 0x000A9054 File Offset: 0x000A7C54
		public virtual bool ReadToNextSibling(string localName, string namespaceURI)
		{
			if (localName == null || localName.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(localName, "localName");
			}
			if (namespaceURI == null)
			{
				throw new ArgumentNullException("namespaceURI");
			}
			localName = this.NameTable.Add(localName);
			namespaceURI = this.NameTable.Add(namespaceURI);
			while (this.SkipSubtree())
			{
				XmlNodeType nodeType = this.NodeType;
				if (nodeType == XmlNodeType.Element && Ref.Equal(localName, this.LocalName) && Ref.Equal(namespaceURI, this.NamespaceURI))
				{
					return true;
				}
				if (nodeType == XmlNodeType.EndElement || this.EOF)
				{
					break;
				}
			}
			return false;
		}

		// Token: 0x06000FF7 RID: 4087 RVA: 0x000A90E1 File Offset: 0x000A7CE1
		public static bool IsName(string str)
		{
			if (str == null)
			{
				throw new NullReferenceException();
			}
			return ValidateNames.IsNameNoNamespaces(str);
		}

		// Token: 0x06000FF8 RID: 4088 RVA: 0x000A90F2 File Offset: 0x000A7CF2
		public static bool IsNameToken(string str)
		{
			if (str == null)
			{
				throw new NullReferenceException();
			}
			return ValidateNames.IsNmtokenNoNamespaces(str);
		}

		// Token: 0x06000FF9 RID: 4089 RVA: 0x000A9104 File Offset: 0x000A7D04
		public virtual string ReadInnerXml()
		{
			if (this.ReadState != ReadState.Interactive)
			{
				return string.Empty;
			}
			if (this.NodeType != XmlNodeType.Attribute && this.NodeType != XmlNodeType.Element)
			{
				this.Read();
				return string.Empty;
			}
			StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
			XmlWriter xmlWriter = this.CreateWriterForInnerOuterXml(stringWriter);
			try
			{
				if (this.NodeType == XmlNodeType.Attribute)
				{
					((XmlTextWriter)xmlWriter).QuoteChar = this.QuoteChar;
					this.WriteAttributeValue(xmlWriter);
				}
				if (this.NodeType == XmlNodeType.Element)
				{
					this.WriteNode(xmlWriter, false);
				}
			}
			finally
			{
				xmlWriter.Close();
			}
			return stringWriter.ToString();
		}

		// Token: 0x06000FFA RID: 4090 RVA: 0x000A91A4 File Offset: 0x000A7DA4
		private void WriteNode(XmlWriter xtw, bool defattr)
		{
			int num = (this.NodeType == XmlNodeType.None) ? -1 : this.Depth;
			while (this.Read() && num < this.Depth)
			{
				switch (this.NodeType)
				{
				case XmlNodeType.Element:
					xtw.WriteStartElement(this.Prefix, this.LocalName, this.NamespaceURI);
					((XmlTextWriter)xtw).QuoteChar = this.QuoteChar;
					xtw.WriteAttributes(this, defattr);
					if (this.IsEmptyElement)
					{
						xtw.WriteEndElement();
					}
					break;
				case XmlNodeType.Text:
					xtw.WriteString(this.Value);
					break;
				case XmlNodeType.CDATA:
					xtw.WriteCData(this.Value);
					break;
				case XmlNodeType.EntityReference:
					xtw.WriteEntityRef(this.Name);
					break;
				case XmlNodeType.ProcessingInstruction:
				case XmlNodeType.XmlDeclaration:
					xtw.WriteProcessingInstruction(this.Name, this.Value);
					break;
				case XmlNodeType.Comment:
					xtw.WriteComment(this.Value);
					break;
				case XmlNodeType.DocumentType:
					xtw.WriteDocType(this.Name, this.GetAttribute("PUBLIC"), this.GetAttribute("SYSTEM"), this.Value);
					break;
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					xtw.WriteWhitespace(this.Value);
					break;
				case XmlNodeType.EndElement:
					xtw.WriteFullEndElement();
					break;
				}
			}
			if (num == this.Depth && this.NodeType == XmlNodeType.EndElement)
			{
				this.Read();
			}
		}

		// Token: 0x06000FFB RID: 4091 RVA: 0x000A9320 File Offset: 0x000A7F20
		private void WriteAttributeValue(XmlWriter xtw)
		{
			string name = this.Name;
			while (this.ReadAttributeValue())
			{
				if (this.NodeType == XmlNodeType.EntityReference)
				{
					xtw.WriteEntityRef(this.Name);
				}
				else
				{
					xtw.WriteString(this.Value);
				}
			}
			this.MoveToAttribute(name);
		}

		// Token: 0x06000FFC RID: 4092 RVA: 0x000A936C File Offset: 0x000A7F6C
		public virtual string ReadOuterXml()
		{
			if (this.ReadState != ReadState.Interactive)
			{
				return string.Empty;
			}
			if (this.NodeType != XmlNodeType.Attribute && this.NodeType != XmlNodeType.Element)
			{
				this.Read();
				return string.Empty;
			}
			StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
			XmlWriter xmlWriter = this.CreateWriterForInnerOuterXml(stringWriter);
			try
			{
				if (this.NodeType == XmlNodeType.Attribute)
				{
					xmlWriter.WriteStartAttribute(this.Prefix, this.LocalName, this.NamespaceURI);
					this.WriteAttributeValue(xmlWriter);
					xmlWriter.WriteEndAttribute();
				}
				else
				{
					xmlWriter.WriteNode(this, false);
				}
			}
			finally
			{
				xmlWriter.Close();
			}
			return stringWriter.ToString();
		}

		// Token: 0x06000FFD RID: 4093 RVA: 0x000A9414 File Offset: 0x000A8014
		private XmlWriter CreateWriterForInnerOuterXml(StringWriter sw)
		{
			XmlTextWriter xmlTextWriter = new XmlTextWriter(sw);
			this.SetNamespacesFlag(xmlTextWriter);
			return xmlTextWriter;
		}

		// Token: 0x06000FFE RID: 4094 RVA: 0x000A9430 File Offset: 0x000A8030
		private void SetNamespacesFlag(XmlTextWriter xtw)
		{
			XmlTextReader xmlTextReader = this as XmlTextReader;
			if (xmlTextReader != null)
			{
				xtw.Namespaces = xmlTextReader.Namespaces;
				return;
			}
			XmlValidatingReader xmlValidatingReader = this as XmlValidatingReader;
			if (xmlValidatingReader != null)
			{
				xtw.Namespaces = xmlValidatingReader.Namespaces;
			}
		}

		// Token: 0x06000FFF RID: 4095 RVA: 0x000A946A File Offset: 0x000A806A
		public virtual XmlReader ReadSubtree()
		{
			if (this.NodeType != XmlNodeType.Element)
			{
				throw new InvalidOperationException(SR.Xml_ReadSubtreeNotOnElement);
			}
			return new XmlSubtreeReader(this);
		}

		// Token: 0x1700060A RID: 1546
		// (get) Token: 0x06001000 RID: 4096 RVA: 0x000A9486 File Offset: 0x000A8086
		public virtual bool HasAttributes
		{
			get
			{
				return this.AttributeCount > 0;
			}
		}

		// Token: 0x06001001 RID: 4097 RVA: 0x000A9491 File Offset: 0x000A8091
		public void Dispose()
		{
			this.Dispose(true);
		}

		// Token: 0x06001002 RID: 4098 RVA: 0x000A949A File Offset: 0x000A809A
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && this.ReadState != ReadState.Closed)
			{
				this.Close();
			}
		}

		// Token: 0x1700060B RID: 1547
		// (get) Token: 0x06001003 RID: 4099 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		internal virtual XmlNamespaceManager NamespaceManager
		{
			get
			{
				return null;
			}
		}

		// Token: 0x06001004 RID: 4100 RVA: 0x000A94AE File Offset: 0x000A80AE
		internal static bool IsTextualNode(XmlNodeType nodeType)
		{
			return (24600L & 1L << (int)(nodeType & (XmlNodeType)31)) != 0L;
		}

		// Token: 0x06001005 RID: 4101 RVA: 0x000A94C2 File Offset: 0x000A80C2
		internal static bool CanReadContentAs(XmlNodeType nodeType)
		{
			return (123324L & 1L << (int)(nodeType & (XmlNodeType)31)) != 0L;
		}

		// Token: 0x06001006 RID: 4102 RVA: 0x000A94D6 File Offset: 0x000A80D6
		internal static bool HasValueInternal(XmlNodeType nodeType)
		{
			return (157084L & 1L << (int)(nodeType & (XmlNodeType)31)) != 0L;
		}

		// Token: 0x06001007 RID: 4103 RVA: 0x000A94EC File Offset: 0x000A80EC
		private bool SkipSubtree()
		{
			this.MoveToElement();
			if (this.NodeType == XmlNodeType.Element && !this.IsEmptyElement)
			{
				int depth = this.Depth;
				while (this.Read() && depth < this.Depth)
				{
				}
				return this.NodeType == XmlNodeType.EndElement && this.Read();
			}
			return this.Read();
		}

		// Token: 0x06001008 RID: 4104 RVA: 0x000A9544 File Offset: 0x000A8144
		internal void CheckElement(string localName, string namespaceURI)
		{
			if (localName == null || localName.Length == 0)
			{
				throw XmlConvert.CreateInvalidNameArgumentException(localName, "localName");
			}
			if (namespaceURI == null)
			{
				throw new ArgumentNullException("namespaceURI");
			}
			if (this.NodeType != XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString(), this as IXmlLineInfo);
			}
			if (this.LocalName != localName || this.NamespaceURI != namespaceURI)
			{
				throw new XmlException(SR.Xml_ElementNotFoundNs, new string[]
				{
					localName,
					namespaceURI
				}, this as IXmlLineInfo);
			}
		}

		// Token: 0x06001009 RID: 4105 RVA: 0x000A95DF File Offset: 0x000A81DF
		internal Exception CreateReadContentAsException(string methodName)
		{
			return XmlReader.CreateReadContentAsException(methodName, this.NodeType, this as IXmlLineInfo);
		}

		// Token: 0x0600100A RID: 4106 RVA: 0x000A95F3 File Offset: 0x000A81F3
		internal Exception CreateReadElementContentAsException(string methodName)
		{
			return XmlReader.CreateReadElementContentAsException(methodName, this.NodeType, this as IXmlLineInfo);
		}

		// Token: 0x0600100B RID: 4107 RVA: 0x000A9607 File Offset: 0x000A8207
		internal bool CanReadContentAs()
		{
			return XmlReader.CanReadContentAs(this.NodeType);
		}

		// Token: 0x0600100C RID: 4108 RVA: 0x000A9614 File Offset: 0x000A8214
		internal static Exception CreateReadContentAsException(string methodName, XmlNodeType nodeType, IXmlLineInfo lineInfo)
		{
			return new InvalidOperationException(XmlReader.AddLineInfo(SR.Format(SR.Xml_InvalidReadContentAs, methodName, nodeType), lineInfo));
		}

		// Token: 0x0600100D RID: 4109 RVA: 0x000A9632 File Offset: 0x000A8232
		internal static Exception CreateReadElementContentAsException(string methodName, XmlNodeType nodeType, IXmlLineInfo lineInfo)
		{
			return new InvalidOperationException(XmlReader.AddLineInfo(SR.Format(SR.Xml_InvalidReadElementContentAs, methodName, nodeType), lineInfo));
		}

		// Token: 0x0600100E RID: 4110 RVA: 0x000A9650 File Offset: 0x000A8250
		private static string AddLineInfo(string message, IXmlLineInfo lineInfo)
		{
			if (lineInfo != null)
			{
				string[] array = new string[]
				{
					lineInfo.LineNumber.ToString(CultureInfo.InvariantCulture),
					lineInfo.LinePosition.ToString(CultureInfo.InvariantCulture)
				};
				string str = message;
				string str2 = " ";
				string xml_ErrorPosition = SR.Xml_ErrorPosition;
				object[] args = array;
				message = str + str2 + SR.Format(xml_ErrorPosition, args);
			}
			return message;
		}

		// Token: 0x0600100F RID: 4111 RVA: 0x000A96B0 File Offset: 0x000A82B0
		internal string InternalReadContentAsString()
		{
			string text = string.Empty;
			StringBuilder stringBuilder = null;
			do
			{
				switch (this.NodeType)
				{
				case XmlNodeType.Attribute:
					goto IL_55;
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					if (text.Length == 0)
					{
						text = this.Value;
						goto IL_9B;
					}
					if (stringBuilder == null)
					{
						stringBuilder = new StringBuilder();
						stringBuilder.Append(text);
					}
					stringBuilder.Append(this.Value);
					goto IL_9B;
				case XmlNodeType.EntityReference:
					if (this.CanResolveEntity)
					{
						this.ResolveEntity();
						goto IL_9B;
					}
					break;
				case XmlNodeType.ProcessingInstruction:
				case XmlNodeType.Comment:
				case XmlNodeType.EndEntity:
					goto IL_9B;
				}
				break;
				IL_9B:;
			}
			while ((this.AttributeCount != 0) ? this.ReadAttributeValue() : this.Read());
			goto IL_B6;
			IL_55:
			return this.Value;
			IL_B6:
			if (stringBuilder != null)
			{
				return stringBuilder.ToString();
			}
			return text;
		}

		// Token: 0x06001010 RID: 4112 RVA: 0x000A9780 File Offset: 0x000A8380
		private bool SetupReadElementContentAsXxx(string methodName)
		{
			if (this.NodeType != XmlNodeType.Element)
			{
				throw this.CreateReadElementContentAsException(methodName);
			}
			bool isEmptyElement = this.IsEmptyElement;
			this.Read();
			if (isEmptyElement)
			{
				return false;
			}
			XmlNodeType nodeType = this.NodeType;
			if (nodeType == XmlNodeType.EndElement)
			{
				this.Read();
				return false;
			}
			if (nodeType == XmlNodeType.Element)
			{
				throw new XmlException(SR.Xml_MixedReadElementContentAs, string.Empty, this as IXmlLineInfo);
			}
			return true;
		}

		// Token: 0x06001011 RID: 4113 RVA: 0x000A97E4 File Offset: 0x000A83E4
		private void FinishReadElementContentAsXxx()
		{
			if (this.NodeType != XmlNodeType.EndElement)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString());
			}
			this.Read();
		}

		// Token: 0x1700060C RID: 1548
		// (get) Token: 0x06001012 RID: 4114 RVA: 0x000A9824 File Offset: 0x000A8424
		internal bool IsDefaultInternal
		{
			get
			{
				if (this.IsDefault)
				{
					return true;
				}
				IXmlSchemaInfo schemaInfo = this.SchemaInfo;
				return schemaInfo != null && schemaInfo.IsDefault;
			}
		}

		// Token: 0x1700060D RID: 1549
		// (get) Token: 0x06001013 RID: 4115 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		internal virtual IDtdInfo DtdInfo
		{
			get
			{
				return null;
			}
		}

		// Token: 0x06001014 RID: 4116 RVA: 0x000A9850 File Offset: 0x000A8450
		internal static ConformanceLevel GetV1ConformanceLevel(XmlReader reader)
		{
			XmlTextReaderImpl xmlTextReaderImpl = XmlReader.GetXmlTextReaderImpl(reader);
			if (xmlTextReaderImpl == null)
			{
				return ConformanceLevel.Document;
			}
			return xmlTextReaderImpl.V1ComformanceLevel;
		}

		// Token: 0x06001015 RID: 4117 RVA: 0x000A9870 File Offset: 0x000A8470
		private static XmlTextReaderImpl GetXmlTextReaderImpl(XmlReader reader)
		{
			XmlTextReaderImpl xmlTextReaderImpl = reader as XmlTextReaderImpl;
			if (xmlTextReaderImpl != null)
			{
				return xmlTextReaderImpl;
			}
			XmlTextReader xmlTextReader = reader as XmlTextReader;
			if (xmlTextReader != null)
			{
				return xmlTextReader.Impl;
			}
			XmlValidatingReaderImpl xmlValidatingReaderImpl = reader as XmlValidatingReaderImpl;
			if (xmlValidatingReaderImpl != null)
			{
				return xmlValidatingReaderImpl.ReaderImpl;
			}
			XmlValidatingReader xmlValidatingReader = reader as XmlValidatingReader;
			if (xmlValidatingReader != null)
			{
				return xmlValidatingReader.Impl.ReaderImpl;
			}
			return null;
		}

		// Token: 0x06001016 RID: 4118 RVA: 0x000A98C2 File Offset: 0x000A84C2
		public static XmlReader Create(string inputUri)
		{
			if (inputUri == null)
			{
				throw new ArgumentNullException("inputUri");
			}
			if (inputUri.Length == 0)
			{
				throw new ArgumentException(SR.XmlConvert_BadUri, "inputUri");
			}
			return new XmlTextReaderImpl(inputUri, XmlReaderSettings.s_defaultReaderSettings, null, new XmlUrlResolver());
		}

		// Token: 0x06001017 RID: 4119 RVA: 0x000A98FB File Offset: 0x000A84FB
		public static XmlReader Create(string inputUri, [Nullable(2)] XmlReaderSettings settings)
		{
			return XmlReader.Create(inputUri, settings, null);
		}

		// Token: 0x06001018 RID: 4120 RVA: 0x000A9905 File Offset: 0x000A8505
		public static XmlReader Create(string inputUri, [Nullable(2)] XmlReaderSettings settings, [Nullable(2)] XmlParserContext inputContext)
		{
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			return settings.CreateReader(inputUri, inputContext);
		}

		// Token: 0x06001019 RID: 4121 RVA: 0x000A9919 File Offset: 0x000A8519
		public static XmlReader Create(Stream input)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			return new XmlTextReaderImpl(input, null, 0, XmlReaderSettings.s_defaultReaderSettings, null, string.Empty, null, false);
		}

		// Token: 0x0600101A RID: 4122 RVA: 0x000A993E File Offset: 0x000A853E
		public static XmlReader Create(Stream input, [Nullable(2)] XmlReaderSettings settings)
		{
			return XmlReader.Create(input, settings, string.Empty);
		}

		// Token: 0x0600101B RID: 4123 RVA: 0x000A994C File Offset: 0x000A854C
		public static XmlReader Create(Stream input, [Nullable(2)] XmlReaderSettings settings, [Nullable(2)] string baseUri)
		{
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			return settings.CreateReader(input, null, baseUri, null);
		}

		// Token: 0x0600101C RID: 4124 RVA: 0x000A9962 File Offset: 0x000A8562
		public static XmlReader Create(Stream input, [Nullable(2)] XmlReaderSettings settings, [Nullable(2)] XmlParserContext inputContext)
		{
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			return settings.CreateReader(input, null, string.Empty, inputContext);
		}

		// Token: 0x0600101D RID: 4125 RVA: 0x000A997C File Offset: 0x000A857C
		public static XmlReader Create(TextReader input)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			return new XmlTextReaderImpl(input, XmlReaderSettings.s_defaultReaderSettings, string.Empty, null);
		}

		// Token: 0x0600101E RID: 4126 RVA: 0x000A999D File Offset: 0x000A859D
		public static XmlReader Create(TextReader input, [Nullable(2)] XmlReaderSettings settings)
		{
			return XmlReader.Create(input, settings, string.Empty);
		}

		// Token: 0x0600101F RID: 4127 RVA: 0x000A99AB File Offset: 0x000A85AB
		public static XmlReader Create(TextReader input, [Nullable(2)] XmlReaderSettings settings, [Nullable(2)] string baseUri)
		{
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			return settings.CreateReader(input, baseUri, null);
		}

		// Token: 0x06001020 RID: 4128 RVA: 0x000A99C0 File Offset: 0x000A85C0
		public static XmlReader Create(TextReader input, [Nullable(2)] XmlReaderSettings settings, [Nullable(2)] XmlParserContext inputContext)
		{
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			return settings.CreateReader(input, string.Empty, inputContext);
		}

		// Token: 0x06001021 RID: 4129 RVA: 0x000A99D9 File Offset: 0x000A85D9
		public static XmlReader Create(XmlReader reader, [Nullable(2)] XmlReaderSettings settings)
		{
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			return settings.CreateReader(reader);
		}

		// Token: 0x06001022 RID: 4130 RVA: 0x000A99EC File Offset: 0x000A85EC
		internal static XmlReader CreateSqlReader(Stream input, XmlReaderSettings settings, XmlParserContext inputContext)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			if (settings == null)
			{
				settings = XmlReaderSettings.s_defaultReaderSettings;
			}
			byte[] array = new byte[XmlReader.CalcBufferSize(input)];
			int num = 0;
			int num2;
			do
			{
				num2 = input.Read(array, num, array.Length - num);
				num += num2;
			}
			while (num2 > 0 && num < 2);
			XmlReader xmlReader;
			if (num >= 2 && array[0] == 223 && array[1] == 255)
			{
				if (inputContext != null)
				{
					throw new ArgumentException(SR.XmlBinary_NoParserContext, "inputContext");
				}
				xmlReader = new XmlSqlBinaryReader(input, array, num, string.Empty, settings.CloseInput, settings);
			}
			else
			{
				xmlReader = new XmlTextReaderImpl(input, array, num, settings, null, string.Empty, inputContext, settings.CloseInput);
			}
			if (settings.ValidationType != ValidationType.None)
			{
				xmlReader = settings.AddValidation(xmlReader);
			}
			if (settings.Async)
			{
				xmlReader = XmlAsyncCheckReader.CreateAsyncCheckWrapper(xmlReader);
			}
			return xmlReader;
		}

		// Token: 0x06001023 RID: 4131 RVA: 0x000A9AB4 File Offset: 0x000A86B4
		internal static int CalcBufferSize(Stream input)
		{
			int num = 4096;
			if (input.CanSeek)
			{
				long length = input.Length;
				if (length < (long)num)
				{
					num = checked((int)length);
				}
				else if (length > 65536L)
				{
					num = 8192;
				}
			}
			return num;
		}

		// Token: 0x1700060E RID: 1550
		// (get) Token: 0x06001024 RID: 4132 RVA: 0x000A9AF0 File Offset: 0x000A86F0
		private object debuggerDisplayProxy
		{
			get
			{
				return new XmlReader.XmlReaderDebuggerDisplayProxy(this);
			}
		}

		// Token: 0x06001025 RID: 4133 RVA: 0x00077CEF File Offset: 0x000768EF
		public virtual Task<string> GetValueAsync()
		{
			throw new NotImplementedException();
		}

		// Token: 0x06001026 RID: 4134 RVA: 0x000A9B00 File Offset: 0x000A8700
		public virtual Task<object> ReadContentAsObjectAsync()
		{
			XmlReader.<ReadContentAsObjectAsync>d__183 <ReadContentAsObjectAsync>d__;
			<ReadContentAsObjectAsync>d__.<>t__builder = AsyncTaskMethodBuilder<object>.Create();
			<ReadContentAsObjectAsync>d__.<>4__this = this;
			<ReadContentAsObjectAsync>d__.<>1__state = -1;
			<ReadContentAsObjectAsync>d__.<>t__builder.Start<XmlReader.<ReadContentAsObjectAsync>d__183>(ref <ReadContentAsObjectAsync>d__);
			return <ReadContentAsObjectAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001027 RID: 4135 RVA: 0x000A9B43 File Offset: 0x000A8743
		public virtual Task<string> ReadContentAsStringAsync()
		{
			if (!this.CanReadContentAs())
			{
				throw this.CreateReadContentAsException("ReadContentAsString");
			}
			return this.InternalReadContentAsStringAsync();
		}

		// Token: 0x06001028 RID: 4136 RVA: 0x000A9B60 File Offset: 0x000A8760
		public virtual Task<object> ReadContentAsAsync(Type returnType, [Nullable(2)] IXmlNamespaceResolver namespaceResolver)
		{
			XmlReader.<ReadContentAsAsync>d__185 <ReadContentAsAsync>d__;
			<ReadContentAsAsync>d__.<>t__builder = AsyncTaskMethodBuilder<object>.Create();
			<ReadContentAsAsync>d__.<>4__this = this;
			<ReadContentAsAsync>d__.returnType = returnType;
			<ReadContentAsAsync>d__.namespaceResolver = namespaceResolver;
			<ReadContentAsAsync>d__.<>1__state = -1;
			<ReadContentAsAsync>d__.<>t__builder.Start<XmlReader.<ReadContentAsAsync>d__185>(ref <ReadContentAsAsync>d__);
			return <ReadContentAsAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001029 RID: 4137 RVA: 0x000A9BB4 File Offset: 0x000A87B4
		public virtual Task<object> ReadElementContentAsObjectAsync()
		{
			XmlReader.<ReadElementContentAsObjectAsync>d__186 <ReadElementContentAsObjectAsync>d__;
			<ReadElementContentAsObjectAsync>d__.<>t__builder = AsyncTaskMethodBuilder<object>.Create();
			<ReadElementContentAsObjectAsync>d__.<>4__this = this;
			<ReadElementContentAsObjectAsync>d__.<>1__state = -1;
			<ReadElementContentAsObjectAsync>d__.<>t__builder.Start<XmlReader.<ReadElementContentAsObjectAsync>d__186>(ref <ReadElementContentAsObjectAsync>d__);
			return <ReadElementContentAsObjectAsync>d__.<>t__builder.Task;
		}

		// Token: 0x0600102A RID: 4138 RVA: 0x000A9BF8 File Offset: 0x000A87F8
		public virtual Task<string> ReadElementContentAsStringAsync()
		{
			XmlReader.<ReadElementContentAsStringAsync>d__187 <ReadElementContentAsStringAsync>d__;
			<ReadElementContentAsStringAsync>d__.<>t__builder = AsyncTaskMethodBuilder<string>.Create();
			<ReadElementContentAsStringAsync>d__.<>4__this = this;
			<ReadElementContentAsStringAsync>d__.<>1__state = -1;
			<ReadElementContentAsStringAsync>d__.<>t__builder.Start<XmlReader.<ReadElementContentAsStringAsync>d__187>(ref <ReadElementContentAsStringAsync>d__);
			return <ReadElementContentAsStringAsync>d__.<>t__builder.Task;
		}

		// Token: 0x0600102B RID: 4139 RVA: 0x000A9C3C File Offset: 0x000A883C
		public virtual Task<object> ReadElementContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver)
		{
			XmlReader.<ReadElementContentAsAsync>d__188 <ReadElementContentAsAsync>d__;
			<ReadElementContentAsAsync>d__.<>t__builder = AsyncTaskMethodBuilder<object>.Create();
			<ReadElementContentAsAsync>d__.<>4__this = this;
			<ReadElementContentAsAsync>d__.returnType = returnType;
			<ReadElementContentAsAsync>d__.namespaceResolver = namespaceResolver;
			<ReadElementContentAsAsync>d__.<>1__state = -1;
			<ReadElementContentAsAsync>d__.<>t__builder.Start<XmlReader.<ReadElementContentAsAsync>d__188>(ref <ReadElementContentAsAsync>d__);
			return <ReadElementContentAsAsync>d__.<>t__builder.Task;
		}

		// Token: 0x0600102C RID: 4140 RVA: 0x00077CEF File Offset: 0x000768EF
		public virtual Task<bool> ReadAsync()
		{
			throw new NotImplementedException();
		}

		// Token: 0x0600102D RID: 4141 RVA: 0x000A9C8F File Offset: 0x000A888F
		public virtual Task SkipAsync()
		{
			if (this.ReadState != ReadState.Interactive)
			{
				return Task.CompletedTask;
			}
			return this.SkipSubtreeAsync();
		}

		// Token: 0x0600102E RID: 4142 RVA: 0x000A88B5 File Offset: 0x000A74B5
		public virtual Task<int> ReadContentAsBase64Async(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadContentAsBase64"));
		}

		// Token: 0x0600102F RID: 4143 RVA: 0x000A88CB File Offset: 0x000A74CB
		public virtual Task<int> ReadElementContentAsBase64Async(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadElementContentAsBase64"));
		}

		// Token: 0x06001030 RID: 4144 RVA: 0x000A88E1 File Offset: 0x000A74E1
		public virtual Task<int> ReadContentAsBinHexAsync(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadContentAsBinHex"));
		}

		// Token: 0x06001031 RID: 4145 RVA: 0x000A88F7 File Offset: 0x000A74F7
		public virtual Task<int> ReadElementContentAsBinHexAsync(byte[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Format(SR.Xml_ReadBinaryContentNotSupported, "ReadElementContentAsBinHex"));
		}

		// Token: 0x06001032 RID: 4146 RVA: 0x000A890D File Offset: 0x000A750D
		public virtual Task<int> ReadValueChunkAsync(char[] buffer, int index, int count)
		{
			throw new NotSupportedException(SR.Xml_ReadValueChunkNotSupported);
		}

		// Token: 0x06001033 RID: 4147 RVA: 0x000A9CA8 File Offset: 0x000A88A8
		public virtual Task<XmlNodeType> MoveToContentAsync()
		{
			XmlReader.<MoveToContentAsync>d__196 <MoveToContentAsync>d__;
			<MoveToContentAsync>d__.<>t__builder = AsyncTaskMethodBuilder<XmlNodeType>.Create();
			<MoveToContentAsync>d__.<>4__this = this;
			<MoveToContentAsync>d__.<>1__state = -1;
			<MoveToContentAsync>d__.<>t__builder.Start<XmlReader.<MoveToContentAsync>d__196>(ref <MoveToContentAsync>d__);
			return <MoveToContentAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001034 RID: 4148 RVA: 0x000A9CEC File Offset: 0x000A88EC
		public virtual Task<string> ReadInnerXmlAsync()
		{
			XmlReader.<ReadInnerXmlAsync>d__197 <ReadInnerXmlAsync>d__;
			<ReadInnerXmlAsync>d__.<>t__builder = AsyncTaskMethodBuilder<string>.Create();
			<ReadInnerXmlAsync>d__.<>4__this = this;
			<ReadInnerXmlAsync>d__.<>1__state = -1;
			<ReadInnerXmlAsync>d__.<>t__builder.Start<XmlReader.<ReadInnerXmlAsync>d__197>(ref <ReadInnerXmlAsync>d__);
			return <ReadInnerXmlAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001035 RID: 4149 RVA: 0x000A9D30 File Offset: 0x000A8930
		private Task WriteNodeAsync(XmlWriter xtw, bool defattr)
		{
			XmlReader.<WriteNodeAsync>d__198 <WriteNodeAsync>d__;
			<WriteNodeAsync>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
			<WriteNodeAsync>d__.<>4__this = this;
			<WriteNodeAsync>d__.xtw = xtw;
			<WriteNodeAsync>d__.defattr = defattr;
			<WriteNodeAsync>d__.<>1__state = -1;
			<WriteNodeAsync>d__.<>t__builder.Start<XmlReader.<WriteNodeAsync>d__198>(ref <WriteNodeAsync>d__);
			return <WriteNodeAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001036 RID: 4150 RVA: 0x000A9D84 File Offset: 0x000A8984
		public virtual Task<string> ReadOuterXmlAsync()
		{
			XmlReader.<ReadOuterXmlAsync>d__199 <ReadOuterXmlAsync>d__;
			<ReadOuterXmlAsync>d__.<>t__builder = AsyncTaskMethodBuilder<string>.Create();
			<ReadOuterXmlAsync>d__.<>4__this = this;
			<ReadOuterXmlAsync>d__.<>1__state = -1;
			<ReadOuterXmlAsync>d__.<>t__builder.Start<XmlReader.<ReadOuterXmlAsync>d__199>(ref <ReadOuterXmlAsync>d__);
			return <ReadOuterXmlAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001037 RID: 4151 RVA: 0x000A9DC8 File Offset: 0x000A89C8
		private Task<bool> SkipSubtreeAsync()
		{
			XmlReader.<SkipSubtreeAsync>d__200 <SkipSubtreeAsync>d__;
			<SkipSubtreeAsync>d__.<>t__builder = AsyncTaskMethodBuilder<bool>.Create();
			<SkipSubtreeAsync>d__.<>4__this = this;
			<SkipSubtreeAsync>d__.<>1__state = -1;
			<SkipSubtreeAsync>d__.<>t__builder.Start<XmlReader.<SkipSubtreeAsync>d__200>(ref <SkipSubtreeAsync>d__);
			return <SkipSubtreeAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001038 RID: 4152 RVA: 0x000A9E0C File Offset: 0x000A8A0C
		internal Task<string> InternalReadContentAsStringAsync()
		{
			XmlReader.<InternalReadContentAsStringAsync>d__201 <InternalReadContentAsStringAsync>d__;
			<InternalReadContentAsStringAsync>d__.<>t__builder = AsyncTaskMethodBuilder<string>.Create();
			<InternalReadContentAsStringAsync>d__.<>4__this = this;
			<InternalReadContentAsStringAsync>d__.<>1__state = -1;
			<InternalReadContentAsStringAsync>d__.<>t__builder.Start<XmlReader.<InternalReadContentAsStringAsync>d__201>(ref <InternalReadContentAsStringAsync>d__);
			return <InternalReadContentAsStringAsync>d__.<>t__builder.Task;
		}

		// Token: 0x06001039 RID: 4153 RVA: 0x000A9E50 File Offset: 0x000A8A50
		private Task<bool> SetupReadElementContentAsXxxAsync(string methodName)
		{
			XmlReader.<SetupReadElementContentAsXxxAsync>d__202 <SetupReadElementContentAsXxxAsync>d__;
			<SetupReadElementContentAsXxxAsync>d__.<>t__builder = AsyncTaskMethodBuilder<bool>.Create();
			<SetupReadElementContentAsXxxAsync>d__.<>4__this = this;
			<SetupReadElementContentAsXxxAsync>d__.methodName = methodName;
			<SetupReadElementContentAsXxxAsync>d__.<>1__state = -1;
			<SetupReadElementContentAsXxxAsync>d__.<>t__builder.Start<XmlReader.<SetupReadElementContentAsXxxAsync>d__202>(ref <SetupReadElementContentAsXxxAsync>d__);
			return <SetupReadElementContentAsXxxAsync>d__.<>t__builder.Task;
		}

		// Token: 0x0600103A RID: 4154 RVA: 0x000A9E9C File Offset: 0x000A8A9C
		private Task FinishReadElementContentAsXxxAsync()
		{
			if (this.NodeType != XmlNodeType.EndElement)
			{
				throw new XmlException(SR.Xml_InvalidNodeType, this.NodeType.ToString());
			}
			return this.ReadAsync();
		}

		// Token: 0x02000152 RID: 338
		[DebuggerDisplay("{ToString()}")]
		private struct XmlReaderDebuggerDisplayProxy
		{
			// Token: 0x0600103C RID: 4156 RVA: 0x000A9ED8 File Offset: 0x000A8AD8
			internal XmlReaderDebuggerDisplayProxy(XmlReader reader)
			{
				this._reader = reader;
			}

			// Token: 0x0600103D RID: 4157 RVA: 0x000A9EE4 File Offset: 0x000A8AE4
			public override string ToString()
			{
				XmlNodeType nodeType = this._reader.NodeType;
				string text = nodeType.ToString();
				switch (nodeType)
				{
				case XmlNodeType.Element:
				case XmlNodeType.EntityReference:
				case XmlNodeType.EndElement:
				case XmlNodeType.EndEntity:
					text = text + ", Name=\"" + this._reader.Name + "\"";
					break;
				case XmlNodeType.Attribute:
				case XmlNodeType.ProcessingInstruction:
					text = string.Concat(new string[]
					{
						text,
						", Name=\"",
						this._reader.Name,
						"\", Value=\"",
						XmlConvert.EscapeValueForDebuggerDisplay(this._reader.Value),
						"\""
					});
					break;
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.Comment:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
				case XmlNodeType.XmlDeclaration:
					text = text + ", Value=\"" + XmlConvert.EscapeValueForDebuggerDisplay(this._reader.Value) + "\"";
					break;
				case XmlNodeType.DocumentType:
					text = text + ", Name=\"" + this._reader.Name + "'";
					text = text + ", SYSTEM=\"" + this._reader.GetAttribute("SYSTEM") + "\"";
					text = text + ", PUBLIC=\"" + this._reader.GetAttribute("PUBLIC") + "\"";
					text = text + ", Value=\"" + XmlConvert.EscapeValueForDebuggerDisplay(this._reader.Value) + "\"";
					break;
				}
				return text;
			}

			// Token: 0x04000652 RID: 1618
			private readonly XmlReader _reader;
		}
	}
}
