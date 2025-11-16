using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Schema;
using System.Xml.XPath;

namespace System.Xml
{
	// Token: 0x02000264 RID: 612
	[Nullable(0)]
	[NullableContext(1)]
	[DebuggerDisplay("{debuggerDisplayProxy}")]
	public abstract class XmlNode : ICloneable, IEnumerable, IXPathNavigable
	{
		// Token: 0x06001AB3 RID: 6835 RVA: 0x000700C8 File Offset: 0x0006ECC8
		internal XmlNode()
		{
		}

		// Token: 0x06001AB4 RID: 6836 RVA: 0x000EED8B File Offset: 0x000ED98B
		internal XmlNode(XmlDocument doc)
		{
			if (doc == null)
			{
				throw new ArgumentException(SR.Xdom_Node_Null_Doc);
			}
			this.parentNode = doc;
		}

		// Token: 0x06001AB5 RID: 6837 RVA: 0x000EEDA8 File Offset: 0x000ED9A8
		[NullableContext(2)]
		public virtual XPathNavigator CreateNavigator()
		{
			XmlDocument xmlDocument = this as XmlDocument;
			if (xmlDocument != null)
			{
				return xmlDocument.CreateNavigator(this);
			}
			XmlDocument ownerDocument = this.OwnerDocument;
			return ownerDocument.CreateNavigator(this);
		}

		// Token: 0x06001AB6 RID: 6838 RVA: 0x000EEDD8 File Offset: 0x000ED9D8
		[return: Nullable(2)]
		public XmlNode SelectSingleNode(string xpath)
		{
			XmlNodeList xmlNodeList = this.SelectNodes(xpath);
			if (xmlNodeList == null)
			{
				return null;
			}
			return xmlNodeList[0];
		}

		// Token: 0x06001AB7 RID: 6839 RVA: 0x000EEDFC File Offset: 0x000ED9FC
		[return: Nullable(2)]
		public XmlNode SelectSingleNode(string xpath, XmlNamespaceManager nsmgr)
		{
			XPathNavigator xpathNavigator = this.CreateNavigator();
			if (xpathNavigator == null)
			{
				return null;
			}
			XPathExpression xpathExpression = xpathNavigator.Compile(xpath);
			xpathExpression.SetContext(nsmgr);
			return new XPathNodeList(xpathNavigator.Select(xpathExpression))[0];
		}

		// Token: 0x06001AB8 RID: 6840 RVA: 0x000EEE38 File Offset: 0x000EDA38
		[return: Nullable(2)]
		public XmlNodeList SelectNodes(string xpath)
		{
			XPathNavigator xpathNavigator = this.CreateNavigator();
			if (xpathNavigator == null)
			{
				return null;
			}
			return new XPathNodeList(xpathNavigator.Select(xpath));
		}

		// Token: 0x06001AB9 RID: 6841 RVA: 0x000EEE60 File Offset: 0x000EDA60
		[return: Nullable(2)]
		public XmlNodeList SelectNodes(string xpath, XmlNamespaceManager nsmgr)
		{
			XPathNavigator xpathNavigator = this.CreateNavigator();
			if (xpathNavigator == null)
			{
				return null;
			}
			XPathExpression xpathExpression = xpathNavigator.Compile(xpath);
			xpathExpression.SetContext(nsmgr);
			return new XPathNodeList(xpathNavigator.Select(xpathExpression));
		}

		// Token: 0x17000852 RID: 2130
		// (get) Token: 0x06001ABA RID: 6842
		public abstract string Name { get; }

		// Token: 0x17000853 RID: 2131
		// (get) Token: 0x06001ABB RID: 6843 RVA: 0x0007140A File Offset: 0x0007000A
		// (set) Token: 0x06001ABC RID: 6844 RVA: 0x000EEE94 File Offset: 0x000EDA94
		[Nullable(2)]
		public virtual string Value
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
			[NullableContext(2)]
			set
			{
				throw new InvalidOperationException(SR.Format(CultureInfo.InvariantCulture, SR.Xdom_Node_SetVal, this.NodeType.ToString()));
			}
		}

		// Token: 0x17000854 RID: 2132
		// (get) Token: 0x06001ABD RID: 6845
		public abstract XmlNodeType NodeType { get; }

		// Token: 0x17000855 RID: 2133
		// (get) Token: 0x06001ABE RID: 6846 RVA: 0x000EEECC File Offset: 0x000EDACC
		[Nullable(2)]
		public virtual XmlNode ParentNode
		{
			[NullableContext(2)]
			get
			{
				if (this.parentNode.NodeType != XmlNodeType.Document)
				{
					return this.parentNode;
				}
				XmlLinkedNode xmlLinkedNode = this.parentNode.FirstChild as XmlLinkedNode;
				if (xmlLinkedNode != null)
				{
					XmlLinkedNode xmlLinkedNode2 = xmlLinkedNode;
					while (xmlLinkedNode2 != this)
					{
						xmlLinkedNode2 = xmlLinkedNode2.next;
						if (xmlLinkedNode2 == null || xmlLinkedNode2 == xmlLinkedNode)
						{
							goto IL_45;
						}
					}
					return this.parentNode;
				}
				IL_45:
				return null;
			}
		}

		// Token: 0x17000856 RID: 2134
		// (get) Token: 0x06001ABF RID: 6847 RVA: 0x000EEF1F File Offset: 0x000EDB1F
		public virtual XmlNodeList ChildNodes
		{
			get
			{
				return new XmlChildNodes(this);
			}
		}

		// Token: 0x17000857 RID: 2135
		// (get) Token: 0x06001AC0 RID: 6848 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public virtual XmlNode PreviousSibling
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x17000858 RID: 2136
		// (get) Token: 0x06001AC1 RID: 6849 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public virtual XmlNode NextSibling
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x17000859 RID: 2137
		// (get) Token: 0x06001AC2 RID: 6850 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public virtual XmlAttributeCollection Attributes
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x1700085A RID: 2138
		// (get) Token: 0x06001AC3 RID: 6851 RVA: 0x000EEF27 File Offset: 0x000EDB27
		[Nullable(2)]
		public virtual XmlDocument OwnerDocument
		{
			[NullableContext(2)]
			get
			{
				if (this.parentNode.NodeType == XmlNodeType.Document)
				{
					return (XmlDocument)this.parentNode;
				}
				return this.parentNode.OwnerDocument;
			}
		}

		// Token: 0x1700085B RID: 2139
		// (get) Token: 0x06001AC4 RID: 6852 RVA: 0x000EEF50 File Offset: 0x000EDB50
		[Nullable(2)]
		public virtual XmlNode FirstChild
		{
			[NullableContext(2)]
			get
			{
				XmlLinkedNode lastNode = this.LastNode;
				if (lastNode != null)
				{
					return lastNode.next;
				}
				return null;
			}
		}

		// Token: 0x1700085C RID: 2140
		// (get) Token: 0x06001AC5 RID: 6853 RVA: 0x000EEF6F File Offset: 0x000EDB6F
		[Nullable(2)]
		public virtual XmlNode LastChild
		{
			[NullableContext(2)]
			get
			{
				return this.LastNode;
			}
		}

		// Token: 0x1700085D RID: 2141
		// (get) Token: 0x06001AC6 RID: 6854 RVA: 0x00070238 File Offset: 0x0006EE38
		internal virtual bool IsContainer
		{
			get
			{
				return false;
			}
		}

		// Token: 0x1700085E RID: 2142
		// (get) Token: 0x06001AC7 RID: 6855 RVA: 0x0007140A File Offset: 0x0007000A
		// (set) Token: 0x06001AC8 RID: 6856 RVA: 0x00070D8A File Offset: 0x0006F98A
		[Nullable(2)]
		internal virtual XmlLinkedNode LastNode
		{
			get
			{
				return null;
			}
			set
			{
			}
		}

		// Token: 0x06001AC9 RID: 6857 RVA: 0x000EEF78 File Offset: 0x000EDB78
		internal bool AncestorNode(XmlNode node)
		{
			XmlNode xmlNode = this.ParentNode;
			while (xmlNode != null && xmlNode != this)
			{
				if (xmlNode == node)
				{
					return true;
				}
				xmlNode = xmlNode.ParentNode;
			}
			return false;
		}

		// Token: 0x06001ACA RID: 6858 RVA: 0x000EEFA4 File Offset: 0x000EDBA4
		internal bool IsConnected()
		{
			XmlNode xmlNode = this.ParentNode;
			while (xmlNode != null && xmlNode.NodeType != XmlNodeType.Document)
			{
				xmlNode = xmlNode.ParentNode;
			}
			return xmlNode != null;
		}

		// Token: 0x06001ACB RID: 6859 RVA: 0x000EEFD4 File Offset: 0x000EDBD4
		[NullableContext(2)]
		public virtual XmlNode InsertBefore([Nullable(1)] XmlNode newChild, XmlNode refChild)
		{
			if (this == newChild || this.AncestorNode(newChild))
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Child);
			}
			if (refChild == null)
			{
				return this.AppendChild(newChild);
			}
			if (!this.IsContainer)
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Contain);
			}
			if (refChild.ParentNode != this)
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Path);
			}
			if (newChild == refChild)
			{
				return newChild;
			}
			XmlDocument ownerDocument = newChild.OwnerDocument;
			XmlDocument ownerDocument2 = this.OwnerDocument;
			if (ownerDocument != null && ownerDocument != ownerDocument2 && ownerDocument != this)
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Context);
			}
			if (!this.CanInsertBefore(newChild, refChild))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Location);
			}
			if (newChild.ParentNode != null)
			{
				newChild.ParentNode.RemoveChild(newChild);
			}
			if (newChild.NodeType == XmlNodeType.DocumentFragment)
			{
				XmlNode firstChild = newChild.FirstChild;
				XmlNode xmlNode = firstChild;
				if (xmlNode != null)
				{
					newChild.RemoveChild(xmlNode);
					this.InsertBefore(xmlNode, refChild);
					this.InsertAfter(newChild, xmlNode);
				}
				return firstChild;
			}
			if (!(newChild is XmlLinkedNode) || !this.IsValidChildType(newChild.NodeType))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_TypeConflict);
			}
			XmlLinkedNode xmlLinkedNode = (XmlLinkedNode)newChild;
			XmlLinkedNode xmlLinkedNode2 = (XmlLinkedNode)refChild;
			string value = newChild.Value;
			XmlNodeChangedEventArgs eventArgs = this.GetEventArgs(newChild, newChild.ParentNode, this, value, value, XmlNodeChangedAction.Insert);
			if (eventArgs != null)
			{
				this.BeforeEvent(eventArgs);
			}
			if (xmlLinkedNode2 == this.FirstChild)
			{
				xmlLinkedNode.next = xmlLinkedNode2;
				this.LastNode.next = xmlLinkedNode;
				xmlLinkedNode.SetParent(this);
				if (xmlLinkedNode.IsText && xmlLinkedNode2.IsText)
				{
					XmlNode.NestTextNodes(xmlLinkedNode, xmlLinkedNode2);
				}
			}
			else
			{
				XmlLinkedNode xmlLinkedNode3 = (XmlLinkedNode)xmlLinkedNode2.PreviousSibling;
				xmlLinkedNode.next = xmlLinkedNode2;
				xmlLinkedNode3.next = xmlLinkedNode;
				xmlLinkedNode.SetParent(this);
				if (xmlLinkedNode3.IsText)
				{
					if (xmlLinkedNode.IsText)
					{
						XmlNode.NestTextNodes(xmlLinkedNode3, xmlLinkedNode);
						if (xmlLinkedNode2.IsText)
						{
							XmlNode.NestTextNodes(xmlLinkedNode, xmlLinkedNode2);
						}
					}
					else if (xmlLinkedNode2.IsText)
					{
						XmlNode.UnnestTextNodes(xmlLinkedNode3, xmlLinkedNode2);
					}
				}
				else if (xmlLinkedNode.IsText && xmlLinkedNode2.IsText)
				{
					XmlNode.NestTextNodes(xmlLinkedNode, xmlLinkedNode2);
				}
			}
			if (eventArgs != null)
			{
				this.AfterEvent(eventArgs);
			}
			return xmlLinkedNode;
		}

		// Token: 0x06001ACC RID: 6860 RVA: 0x000EF1D4 File Offset: 0x000EDDD4
		[NullableContext(2)]
		public virtual XmlNode InsertAfter([Nullable(1)] XmlNode newChild, XmlNode refChild)
		{
			if (this == newChild || this.AncestorNode(newChild))
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Child);
			}
			if (refChild == null)
			{
				return this.PrependChild(newChild);
			}
			if (!this.IsContainer)
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Contain);
			}
			if (refChild.ParentNode != this)
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Path);
			}
			if (newChild == refChild)
			{
				return newChild;
			}
			XmlDocument ownerDocument = newChild.OwnerDocument;
			XmlDocument ownerDocument2 = this.OwnerDocument;
			if (ownerDocument != null && ownerDocument != ownerDocument2 && ownerDocument != this)
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Context);
			}
			if (!this.CanInsertAfter(newChild, refChild))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Location);
			}
			if (newChild.ParentNode != null)
			{
				newChild.ParentNode.RemoveChild(newChild);
			}
			if (newChild.NodeType == XmlNodeType.DocumentFragment)
			{
				XmlNode refChild2 = refChild;
				XmlNode firstChild = newChild.FirstChild;
				XmlNode nextSibling;
				for (XmlNode xmlNode = firstChild; xmlNode != null; xmlNode = nextSibling)
				{
					nextSibling = xmlNode.NextSibling;
					newChild.RemoveChild(xmlNode);
					this.InsertAfter(xmlNode, refChild2);
					refChild2 = xmlNode;
				}
				return firstChild;
			}
			if (!(newChild is XmlLinkedNode) || !this.IsValidChildType(newChild.NodeType))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_TypeConflict);
			}
			XmlLinkedNode xmlLinkedNode = (XmlLinkedNode)newChild;
			XmlLinkedNode xmlLinkedNode2 = (XmlLinkedNode)refChild;
			string value = newChild.Value;
			XmlNodeChangedEventArgs eventArgs = this.GetEventArgs(newChild, newChild.ParentNode, this, value, value, XmlNodeChangedAction.Insert);
			if (eventArgs != null)
			{
				this.BeforeEvent(eventArgs);
			}
			if (xmlLinkedNode2 == this.LastNode)
			{
				xmlLinkedNode.next = xmlLinkedNode2.next;
				xmlLinkedNode2.next = xmlLinkedNode;
				this.LastNode = xmlLinkedNode;
				xmlLinkedNode.SetParent(this);
				if (xmlLinkedNode2.IsText && xmlLinkedNode.IsText)
				{
					XmlNode.NestTextNodes(xmlLinkedNode2, xmlLinkedNode);
				}
			}
			else
			{
				XmlLinkedNode next = xmlLinkedNode2.next;
				xmlLinkedNode.next = next;
				xmlLinkedNode2.next = xmlLinkedNode;
				xmlLinkedNode.SetParent(this);
				if (xmlLinkedNode2.IsText)
				{
					if (xmlLinkedNode.IsText)
					{
						XmlNode.NestTextNodes(xmlLinkedNode2, xmlLinkedNode);
						if (next.IsText)
						{
							XmlNode.NestTextNodes(xmlLinkedNode, next);
						}
					}
					else if (next.IsText)
					{
						XmlNode.UnnestTextNodes(xmlLinkedNode2, next);
					}
				}
				else if (xmlLinkedNode.IsText && next.IsText)
				{
					XmlNode.NestTextNodes(xmlLinkedNode, next);
				}
			}
			if (eventArgs != null)
			{
				this.AfterEvent(eventArgs);
			}
			return xmlLinkedNode;
		}

		// Token: 0x06001ACD RID: 6861 RVA: 0x000EF3E8 File Offset: 0x000EDFE8
		public virtual XmlNode ReplaceChild(XmlNode newChild, XmlNode oldChild)
		{
			XmlNode nextSibling = oldChild.NextSibling;
			this.RemoveChild(oldChild);
			XmlNode xmlNode = this.InsertBefore(newChild, nextSibling);
			return oldChild;
		}

		// Token: 0x06001ACE RID: 6862 RVA: 0x000EF410 File Offset: 0x000EE010
		public virtual XmlNode RemoveChild(XmlNode oldChild)
		{
			if (!this.IsContainer)
			{
				throw new InvalidOperationException(SR.Xdom_Node_Remove_Contain);
			}
			if (oldChild.ParentNode != this)
			{
				throw new ArgumentException(SR.Xdom_Node_Remove_Child);
			}
			XmlLinkedNode xmlLinkedNode = (XmlLinkedNode)oldChild;
			string value = xmlLinkedNode.Value;
			XmlNodeChangedEventArgs eventArgs = this.GetEventArgs(xmlLinkedNode, this, null, value, value, XmlNodeChangedAction.Remove);
			if (eventArgs != null)
			{
				this.BeforeEvent(eventArgs);
			}
			XmlLinkedNode lastNode = this.LastNode;
			if (xmlLinkedNode == this.FirstChild)
			{
				if (xmlLinkedNode == lastNode)
				{
					this.LastNode = null;
					xmlLinkedNode.next = null;
					xmlLinkedNode.SetParent(null);
				}
				else
				{
					XmlLinkedNode next = xmlLinkedNode.next;
					if (next.IsText && xmlLinkedNode.IsText)
					{
						XmlNode.UnnestTextNodes(xmlLinkedNode, next);
					}
					lastNode.next = next;
					xmlLinkedNode.next = null;
					xmlLinkedNode.SetParent(null);
				}
			}
			else if (xmlLinkedNode == lastNode)
			{
				XmlLinkedNode xmlLinkedNode2 = (XmlLinkedNode)xmlLinkedNode.PreviousSibling;
				xmlLinkedNode2.next = xmlLinkedNode.next;
				this.LastNode = xmlLinkedNode2;
				xmlLinkedNode.next = null;
				xmlLinkedNode.SetParent(null);
			}
			else
			{
				XmlLinkedNode xmlLinkedNode3 = (XmlLinkedNode)xmlLinkedNode.PreviousSibling;
				XmlLinkedNode next2 = xmlLinkedNode.next;
				if (next2.IsText)
				{
					if (xmlLinkedNode3.IsText)
					{
						XmlNode.NestTextNodes(xmlLinkedNode3, next2);
					}
					else if (xmlLinkedNode.IsText)
					{
						XmlNode.UnnestTextNodes(xmlLinkedNode, next2);
					}
				}
				xmlLinkedNode3.next = next2;
				xmlLinkedNode.next = null;
				xmlLinkedNode.SetParent(null);
			}
			if (eventArgs != null)
			{
				this.AfterEvent(eventArgs);
			}
			return oldChild;
		}

		// Token: 0x06001ACF RID: 6863 RVA: 0x000EF56D File Offset: 0x000EE16D
		[return: Nullable(2)]
		public virtual XmlNode PrependChild(XmlNode newChild)
		{
			return this.InsertBefore(newChild, this.FirstChild);
		}

		// Token: 0x06001AD0 RID: 6864 RVA: 0x000EF57C File Offset: 0x000EE17C
		[return: Nullable(2)]
		public virtual XmlNode AppendChild(XmlNode newChild)
		{
			XmlDocument xmlDocument = this.OwnerDocument;
			if (xmlDocument == null)
			{
				xmlDocument = (this as XmlDocument);
			}
			if (!this.IsContainer)
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Contain);
			}
			if (this == newChild || this.AncestorNode(newChild))
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Child);
			}
			if (newChild.ParentNode != null)
			{
				newChild.ParentNode.RemoveChild(newChild);
			}
			XmlDocument ownerDocument = newChild.OwnerDocument;
			if (ownerDocument != null && ownerDocument != xmlDocument && ownerDocument != this)
			{
				throw new ArgumentException(SR.Xdom_Node_Insert_Context);
			}
			if (newChild.NodeType == XmlNodeType.DocumentFragment)
			{
				XmlNode firstChild = newChild.FirstChild;
				XmlNode nextSibling;
				for (XmlNode xmlNode = firstChild; xmlNode != null; xmlNode = nextSibling)
				{
					nextSibling = xmlNode.NextSibling;
					newChild.RemoveChild(xmlNode);
					this.AppendChild(xmlNode);
				}
				return firstChild;
			}
			if (!(newChild is XmlLinkedNode) || !this.IsValidChildType(newChild.NodeType))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_TypeConflict);
			}
			if (!this.CanInsertAfter(newChild, this.LastChild))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Insert_Location);
			}
			string value = newChild.Value;
			XmlNodeChangedEventArgs eventArgs = this.GetEventArgs(newChild, newChild.ParentNode, this, value, value, XmlNodeChangedAction.Insert);
			if (eventArgs != null)
			{
				this.BeforeEvent(eventArgs);
			}
			XmlLinkedNode lastNode = this.LastNode;
			XmlLinkedNode xmlLinkedNode = (XmlLinkedNode)newChild;
			if (lastNode == null)
			{
				xmlLinkedNode.next = xmlLinkedNode;
				this.LastNode = xmlLinkedNode;
				xmlLinkedNode.SetParent(this);
			}
			else
			{
				xmlLinkedNode.next = lastNode.next;
				lastNode.next = xmlLinkedNode;
				this.LastNode = xmlLinkedNode;
				xmlLinkedNode.SetParent(this);
				if (lastNode.IsText && xmlLinkedNode.IsText)
				{
					XmlNode.NestTextNodes(lastNode, xmlLinkedNode);
				}
			}
			if (eventArgs != null)
			{
				this.AfterEvent(eventArgs);
			}
			return xmlLinkedNode;
		}

		// Token: 0x06001AD1 RID: 6865 RVA: 0x000EF710 File Offset: 0x000EE310
		internal virtual XmlNode AppendChildForLoad(XmlNode newChild, XmlDocument doc)
		{
			XmlNodeChangedEventArgs insertEventArgsForLoad = doc.GetInsertEventArgsForLoad(newChild, this);
			if (insertEventArgsForLoad != null)
			{
				doc.BeforeEvent(insertEventArgsForLoad);
			}
			XmlLinkedNode lastNode = this.LastNode;
			XmlLinkedNode xmlLinkedNode = (XmlLinkedNode)newChild;
			if (lastNode == null)
			{
				xmlLinkedNode.next = xmlLinkedNode;
				this.LastNode = xmlLinkedNode;
				xmlLinkedNode.SetParentForLoad(this);
			}
			else
			{
				xmlLinkedNode.next = lastNode.next;
				lastNode.next = xmlLinkedNode;
				this.LastNode = xmlLinkedNode;
				if (lastNode.IsText && xmlLinkedNode.IsText)
				{
					XmlNode.NestTextNodes(lastNode, xmlLinkedNode);
				}
				else
				{
					xmlLinkedNode.SetParentForLoad(this);
				}
			}
			if (insertEventArgsForLoad != null)
			{
				doc.AfterEvent(insertEventArgsForLoad);
			}
			return xmlLinkedNode;
		}

		// Token: 0x06001AD2 RID: 6866 RVA: 0x00070238 File Offset: 0x0006EE38
		internal virtual bool IsValidChildType(XmlNodeType type)
		{
			return false;
		}

		// Token: 0x06001AD3 RID: 6867 RVA: 0x0007108E File Offset: 0x0006FC8E
		internal virtual bool CanInsertBefore(XmlNode newChild, XmlNode refChild)
		{
			return true;
		}

		// Token: 0x06001AD4 RID: 6868 RVA: 0x0007108E File Offset: 0x0006FC8E
		internal virtual bool CanInsertAfter(XmlNode newChild, XmlNode refChild)
		{
			return true;
		}

		// Token: 0x1700085F RID: 2143
		// (get) Token: 0x06001AD5 RID: 6869 RVA: 0x000EF79D File Offset: 0x000EE39D
		public virtual bool HasChildNodes
		{
			get
			{
				return this.LastNode != null;
			}
		}

		// Token: 0x06001AD6 RID: 6870
		public abstract XmlNode CloneNode(bool deep);

		// Token: 0x06001AD7 RID: 6871 RVA: 0x000EF7A8 File Offset: 0x000EE3A8
		internal virtual void CopyChildren(XmlDocument doc, XmlNode container, bool deep)
		{
			for (XmlNode xmlNode = container.FirstChild; xmlNode != null; xmlNode = xmlNode.NextSibling)
			{
				this.AppendChildForLoad(xmlNode.CloneNode(deep), doc);
			}
		}

		// Token: 0x06001AD8 RID: 6872 RVA: 0x000EF7D8 File Offset: 0x000EE3D8
		public virtual void Normalize()
		{
			XmlNode xmlNode = null;
			StringBuilder stringBuilder = StringBuilderCache.Acquire(16);
			XmlNode xmlNode2 = this.FirstChild;
			while (xmlNode2 != null)
			{
				XmlNode nextSibling = xmlNode2.NextSibling;
				XmlNodeType nodeType = xmlNode2.NodeType;
				if (nodeType == XmlNodeType.Element)
				{
					xmlNode2.Normalize();
					goto IL_6F;
				}
				if (nodeType != XmlNodeType.Text && nodeType - XmlNodeType.Whitespace > 1)
				{
					goto IL_6F;
				}
				stringBuilder.Append(xmlNode2.Value);
				XmlNode xmlNode3 = this.NormalizeWinner(xmlNode, xmlNode2);
				if (xmlNode3 == xmlNode)
				{
					this.RemoveChild(xmlNode2);
				}
				else
				{
					if (xmlNode != null)
					{
						this.RemoveChild(xmlNode);
					}
					xmlNode = xmlNode2;
				}
				IL_8E:
				xmlNode2 = nextSibling;
				continue;
				IL_6F:
				if (xmlNode != null)
				{
					xmlNode.Value = stringBuilder.ToString();
					xmlNode = null;
				}
				stringBuilder.Remove(0, stringBuilder.Length);
				goto IL_8E;
			}
			if (xmlNode != null && stringBuilder.Length > 0)
			{
				xmlNode.Value = stringBuilder.ToString();
			}
			StringBuilderCache.Release(stringBuilder);
		}

		// Token: 0x06001AD9 RID: 6873 RVA: 0x000EF898 File Offset: 0x000EE498
		private XmlNode NormalizeWinner(XmlNode firstNode, XmlNode secondNode)
		{
			if (firstNode == null)
			{
				return secondNode;
			}
			if (firstNode.NodeType == XmlNodeType.Text)
			{
				return firstNode;
			}
			if (secondNode.NodeType == XmlNodeType.Text)
			{
				return secondNode;
			}
			if (firstNode.NodeType == XmlNodeType.SignificantWhitespace)
			{
				return firstNode;
			}
			if (secondNode.NodeType == XmlNodeType.SignificantWhitespace)
			{
				return secondNode;
			}
			if (firstNode.NodeType == XmlNodeType.Whitespace)
			{
				return firstNode;
			}
			if (secondNode.NodeType == XmlNodeType.Whitespace)
			{
				return secondNode;
			}
			return null;
		}

		// Token: 0x06001ADA RID: 6874 RVA: 0x000ED1BE File Offset: 0x000EBDBE
		public virtual bool Supports(string feature, string version)
		{
			return string.Equals("XML", feature, StringComparison.OrdinalIgnoreCase) && (version == null || version == "1.0" || version == "2.0");
		}

		// Token: 0x17000860 RID: 2144
		// (get) Token: 0x06001ADB RID: 6875 RVA: 0x00070E99 File Offset: 0x0006FA99
		public virtual string NamespaceURI
		{
			get
			{
				return string.Empty;
			}
		}

		// Token: 0x17000861 RID: 2145
		// (get) Token: 0x06001ADC RID: 6876 RVA: 0x00070E99 File Offset: 0x0006FA99
		// (set) Token: 0x06001ADD RID: 6877 RVA: 0x00070D8A File Offset: 0x0006F98A
		public virtual string Prefix
		{
			get
			{
				return string.Empty;
			}
			set
			{
			}
		}

		// Token: 0x17000862 RID: 2146
		// (get) Token: 0x06001ADE RID: 6878
		public abstract string LocalName { get; }

		// Token: 0x17000863 RID: 2147
		// (get) Token: 0x06001ADF RID: 6879 RVA: 0x000EF8F4 File Offset: 0x000EE4F4
		public virtual bool IsReadOnly
		{
			get
			{
				XmlDocument ownerDocument = this.OwnerDocument;
				return XmlNode.HasReadOnlyParent(this);
			}
		}

		// Token: 0x06001AE0 RID: 6880 RVA: 0x000EF910 File Offset: 0x000EE510
		internal static bool HasReadOnlyParent(XmlNode n)
		{
			while (n != null)
			{
				XmlNodeType nodeType = n.NodeType;
				if (nodeType != XmlNodeType.Attribute)
				{
					if (nodeType - XmlNodeType.EntityReference <= 1)
					{
						return true;
					}
					n = n.ParentNode;
				}
				else
				{
					n = ((XmlAttribute)n).OwnerElement;
				}
			}
			return false;
		}

		// Token: 0x06001AE1 RID: 6881 RVA: 0x000EF94D File Offset: 0x000EE54D
		public virtual XmlNode Clone()
		{
			return this.CloneNode(true);
		}

		// Token: 0x06001AE2 RID: 6882 RVA: 0x000EF94D File Offset: 0x000EE54D
		object ICloneable.Clone()
		{
			return this.CloneNode(true);
		}

		// Token: 0x06001AE3 RID: 6883 RVA: 0x000EF956 File Offset: 0x000EE556
		IEnumerator IEnumerable.GetEnumerator()
		{
			return new XmlChildEnumerator(this);
		}

		// Token: 0x06001AE4 RID: 6884 RVA: 0x000EF956 File Offset: 0x000EE556
		public IEnumerator GetEnumerator()
		{
			return new XmlChildEnumerator(this);
		}

		// Token: 0x06001AE5 RID: 6885 RVA: 0x000EF960 File Offset: 0x000EE560
		private void AppendChildText(StringBuilder builder)
		{
			for (XmlNode xmlNode = this.FirstChild; xmlNode != null; xmlNode = xmlNode.NextSibling)
			{
				if (xmlNode.FirstChild == null)
				{
					if (xmlNode.NodeType == XmlNodeType.Text || xmlNode.NodeType == XmlNodeType.CDATA || xmlNode.NodeType == XmlNodeType.Whitespace || xmlNode.NodeType == XmlNodeType.SignificantWhitespace)
					{
						builder.Append(xmlNode.InnerText);
					}
				}
				else
				{
					xmlNode.AppendChildText(builder);
				}
			}
		}

		// Token: 0x17000864 RID: 2148
		// (get) Token: 0x06001AE6 RID: 6886 RVA: 0x000EF9C4 File Offset: 0x000EE5C4
		// (set) Token: 0x06001AE7 RID: 6887 RVA: 0x000EFA1C File Offset: 0x000EE61C
		public virtual string InnerText
		{
			get
			{
				XmlNode firstChild = this.FirstChild;
				if (firstChild == null)
				{
					return string.Empty;
				}
				if (firstChild.NextSibling == null)
				{
					XmlNodeType nodeType = firstChild.NodeType;
					if (nodeType - XmlNodeType.Text <= 1 || nodeType - XmlNodeType.Whitespace <= 1)
					{
						return firstChild.Value;
					}
				}
				StringBuilder stringBuilder = StringBuilderCache.Acquire(16);
				this.AppendChildText(stringBuilder);
				return StringBuilderCache.GetStringAndRelease(stringBuilder);
			}
			set
			{
				XmlNode firstChild = this.FirstChild;
				if (firstChild != null && firstChild.NextSibling == null && firstChild.NodeType == XmlNodeType.Text)
				{
					firstChild.Value = value;
					return;
				}
				this.RemoveAll();
				this.AppendChild(this.OwnerDocument.CreateTextNode(value));
			}
		}

		// Token: 0x17000865 RID: 2149
		// (get) Token: 0x06001AE8 RID: 6888 RVA: 0x000EFA68 File Offset: 0x000EE668
		public virtual string OuterXml
		{
			get
			{
				StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
				XmlDOMTextWriter xmlDOMTextWriter = new XmlDOMTextWriter(stringWriter);
				try
				{
					this.WriteTo(xmlDOMTextWriter);
				}
				finally
				{
					xmlDOMTextWriter.Close();
				}
				return stringWriter.ToString();
			}
		}

		// Token: 0x17000866 RID: 2150
		// (get) Token: 0x06001AE9 RID: 6889 RVA: 0x000EFAB0 File Offset: 0x000EE6B0
		// (set) Token: 0x06001AEA RID: 6890 RVA: 0x000ECFEA File Offset: 0x000EBBEA
		public virtual string InnerXml
		{
			get
			{
				StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
				XmlDOMTextWriter xmlDOMTextWriter = new XmlDOMTextWriter(stringWriter);
				try
				{
					this.WriteContentTo(xmlDOMTextWriter);
				}
				finally
				{
					xmlDOMTextWriter.Close();
				}
				return stringWriter.ToString();
			}
			set
			{
				throw new InvalidOperationException(SR.Xdom_Set_InnerXml);
			}
		}

		// Token: 0x17000867 RID: 2151
		// (get) Token: 0x06001AEB RID: 6891 RVA: 0x000EFAF8 File Offset: 0x000EE6F8
		public virtual IXmlSchemaInfo SchemaInfo
		{
			get
			{
				return XmlDocument.NotKnownSchemaInfo;
			}
		}

		// Token: 0x17000868 RID: 2152
		// (get) Token: 0x06001AEC RID: 6892 RVA: 0x000EFB00 File Offset: 0x000EE700
		public virtual string BaseURI
		{
			get
			{
				for (XmlNode xmlNode = this.ParentNode; xmlNode != null; xmlNode = xmlNode.ParentNode)
				{
					XmlNodeType nodeType = xmlNode.NodeType;
					if (nodeType == XmlNodeType.EntityReference)
					{
						return ((XmlEntityReference)xmlNode).ChildBaseURI;
					}
					if (nodeType == XmlNodeType.Document || nodeType == XmlNodeType.Entity || nodeType == XmlNodeType.Attribute)
					{
						return xmlNode.BaseURI;
					}
				}
				return string.Empty;
			}
		}

		// Token: 0x06001AED RID: 6893
		public abstract void WriteTo(XmlWriter w);

		// Token: 0x06001AEE RID: 6894
		public abstract void WriteContentTo(XmlWriter w);

		// Token: 0x06001AEF RID: 6895 RVA: 0x000EFB50 File Offset: 0x000EE750
		public virtual void RemoveAll()
		{
			XmlNode nextSibling;
			for (XmlNode xmlNode = this.FirstChild; xmlNode != null; xmlNode = nextSibling)
			{
				nextSibling = xmlNode.NextSibling;
				this.RemoveChild(xmlNode);
			}
		}

		// Token: 0x17000869 RID: 2153
		// (get) Token: 0x06001AF0 RID: 6896 RVA: 0x000EFB7C File Offset: 0x000EE77C
		internal XmlDocument Document
		{
			get
			{
				if (this.NodeType == XmlNodeType.Document)
				{
					return (XmlDocument)this;
				}
				return this.OwnerDocument;
			}
		}

		// Token: 0x06001AF1 RID: 6897 RVA: 0x000EFB98 File Offset: 0x000EE798
		public virtual string GetNamespaceOfPrefix(string prefix)
		{
			string namespaceOfPrefixStrict = this.GetNamespaceOfPrefixStrict(prefix);
			return namespaceOfPrefixStrict ?? string.Empty;
		}

		// Token: 0x06001AF2 RID: 6898 RVA: 0x000EFBB8 File Offset: 0x000EE7B8
		internal string GetNamespaceOfPrefixStrict(string prefix)
		{
			XmlDocument document = this.Document;
			if (document != null)
			{
				string text = document.NameTable.Get(prefix);
				if (text == null)
				{
					return null;
				}
				XmlNode xmlNode = this;
				while (xmlNode != null)
				{
					if (xmlNode.NodeType == XmlNodeType.Element)
					{
						XmlElement xmlElement = (XmlElement)xmlNode;
						if (xmlElement.HasAttributes)
						{
							XmlAttributeCollection attributes = xmlElement.Attributes;
							if (text.Length == 0)
							{
								for (int i = 0; i < attributes.Count; i++)
								{
									XmlAttribute xmlAttribute = attributes[i];
									if (xmlAttribute.Prefix.Length == 0 && Ref.Equal(xmlAttribute.LocalName, document.strXmlns))
									{
										return xmlAttribute.Value;
									}
								}
							}
							else
							{
								for (int j = 0; j < attributes.Count; j++)
								{
									XmlAttribute xmlAttribute2 = attributes[j];
									if (Ref.Equal(xmlAttribute2.Prefix, document.strXmlns))
									{
										if (Ref.Equal(xmlAttribute2.LocalName, text))
										{
											return xmlAttribute2.Value;
										}
									}
									else if (Ref.Equal(xmlAttribute2.Prefix, text))
									{
										return xmlAttribute2.NamespaceURI;
									}
								}
							}
						}
						if (Ref.Equal(xmlNode.Prefix, text))
						{
							return xmlNode.NamespaceURI;
						}
						xmlNode = xmlNode.ParentNode;
					}
					else if (xmlNode.NodeType == XmlNodeType.Attribute)
					{
						xmlNode = ((XmlAttribute)xmlNode).OwnerElement;
					}
					else
					{
						xmlNode = xmlNode.ParentNode;
					}
				}
				if (Ref.Equal(document.strXml, text))
				{
					return document.strReservedXml;
				}
				if (Ref.Equal(document.strXmlns, text))
				{
					return document.strReservedXmlns;
				}
			}
			return null;
		}

		// Token: 0x06001AF3 RID: 6899 RVA: 0x000EFD38 File Offset: 0x000EE938
		public virtual string GetPrefixOfNamespace(string namespaceURI)
		{
			string prefixOfNamespaceStrict = this.GetPrefixOfNamespaceStrict(namespaceURI);
			if (prefixOfNamespaceStrict == null)
			{
				return string.Empty;
			}
			return prefixOfNamespaceStrict;
		}

		// Token: 0x06001AF4 RID: 6900 RVA: 0x000EFD58 File Offset: 0x000EE958
		internal string GetPrefixOfNamespaceStrict(string namespaceURI)
		{
			XmlDocument document = this.Document;
			if (document != null)
			{
				namespaceURI = document.NameTable.Add(namespaceURI);
				XmlNode xmlNode = this;
				while (xmlNode != null)
				{
					if (xmlNode.NodeType == XmlNodeType.Element)
					{
						XmlElement xmlElement = (XmlElement)xmlNode;
						if (xmlElement.HasAttributes)
						{
							XmlAttributeCollection attributes = xmlElement.Attributes;
							for (int i = 0; i < attributes.Count; i++)
							{
								XmlAttribute xmlAttribute = attributes[i];
								if (xmlAttribute.Prefix.Length == 0)
								{
									if (Ref.Equal(xmlAttribute.LocalName, document.strXmlns) && xmlAttribute.Value == namespaceURI)
									{
										return string.Empty;
									}
								}
								else if (Ref.Equal(xmlAttribute.Prefix, document.strXmlns))
								{
									if (xmlAttribute.Value == namespaceURI)
									{
										return xmlAttribute.LocalName;
									}
								}
								else if (Ref.Equal(xmlAttribute.NamespaceURI, namespaceURI))
								{
									return xmlAttribute.Prefix;
								}
							}
						}
						if (Ref.Equal(xmlNode.NamespaceURI, namespaceURI))
						{
							return xmlNode.Prefix;
						}
						xmlNode = xmlNode.ParentNode;
					}
					else if (xmlNode.NodeType == XmlNodeType.Attribute)
					{
						xmlNode = ((XmlAttribute)xmlNode).OwnerElement;
					}
					else
					{
						xmlNode = xmlNode.ParentNode;
					}
				}
				if (Ref.Equal(document.strReservedXml, namespaceURI))
				{
					return document.strXml;
				}
				if (Ref.Equal(document.strReservedXmlns, namespaceURI))
				{
					return document.strXmlns;
				}
			}
			return null;
		}

		// Token: 0x1700086A RID: 2154
		[Nullable(2)]
		public virtual XmlElement this[string name]
		{
			[return: Nullable(2)]
			get
			{
				for (XmlNode xmlNode = this.FirstChild; xmlNode != null; xmlNode = xmlNode.NextSibling)
				{
					if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == name)
					{
						return (XmlElement)xmlNode;
					}
				}
				return null;
			}
		}

		// Token: 0x1700086B RID: 2155
		[Nullable(2)]
		public virtual XmlElement this[string localname, string ns]
		{
			[return: Nullable(2)]
			get
			{
				for (XmlNode xmlNode = this.FirstChild; xmlNode != null; xmlNode = xmlNode.NextSibling)
				{
					if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.LocalName == localname && xmlNode.NamespaceURI == ns)
					{
						return (XmlElement)xmlNode;
					}
				}
				return null;
			}
		}

		// Token: 0x06001AF7 RID: 6903 RVA: 0x000EFF45 File Offset: 0x000EEB45
		internal virtual void SetParent(XmlNode node)
		{
			if (node == null)
			{
				this.parentNode = this.OwnerDocument;
				return;
			}
			this.parentNode = node;
		}

		// Token: 0x06001AF8 RID: 6904 RVA: 0x000E7BBE File Offset: 0x000E67BE
		internal virtual void SetParentForLoad(XmlNode node)
		{
			this.parentNode = node;
		}

		// Token: 0x06001AF9 RID: 6905 RVA: 0x000EFF60 File Offset: 0x000EEB60
		internal static void SplitName(string name, out string prefix, out string localName)
		{
			int num = name.IndexOf(':');
			if (-1 == num || num == 0 || name.Length - 1 == num)
			{
				prefix = string.Empty;
				localName = name;
				return;
			}
			prefix = name.Substring(0, num);
			localName = name.Substring(num + 1);
		}

		// Token: 0x06001AFA RID: 6906 RVA: 0x000EFFA8 File Offset: 0x000EEBA8
		internal virtual XmlNode FindChild(XmlNodeType type)
		{
			for (XmlNode xmlNode = this.FirstChild; xmlNode != null; xmlNode = xmlNode.NextSibling)
			{
				if (xmlNode.NodeType == type)
				{
					return xmlNode;
				}
			}
			return null;
		}

		// Token: 0x06001AFB RID: 6907 RVA: 0x000EFFD4 File Offset: 0x000EEBD4
		internal virtual XmlNodeChangedEventArgs GetEventArgs(XmlNode node, XmlNode oldParent, XmlNode newParent, string oldValue, string newValue, XmlNodeChangedAction action)
		{
			XmlDocument ownerDocument = this.OwnerDocument;
			if (ownerDocument == null)
			{
				return null;
			}
			if (!ownerDocument.IsLoading && ((newParent != null && newParent.IsReadOnly) || (oldParent != null && oldParent.IsReadOnly)))
			{
				throw new InvalidOperationException(SR.Xdom_Node_Modify_ReadOnly);
			}
			return ownerDocument.GetEventArgs(node, oldParent, newParent, oldValue, newValue, action);
		}

		// Token: 0x06001AFC RID: 6908 RVA: 0x000F0025 File Offset: 0x000EEC25
		internal virtual void BeforeEvent(XmlNodeChangedEventArgs args)
		{
			if (args != null)
			{
				this.OwnerDocument.BeforeEvent(args);
			}
		}

		// Token: 0x06001AFD RID: 6909 RVA: 0x000F0036 File Offset: 0x000EEC36
		internal virtual void AfterEvent(XmlNodeChangedEventArgs args)
		{
			if (args != null)
			{
				this.OwnerDocument.AfterEvent(args);
			}
		}

		// Token: 0x1700086C RID: 2156
		// (get) Token: 0x06001AFE RID: 6910 RVA: 0x000F0048 File Offset: 0x000EEC48
		internal virtual XmlSpace XmlSpace
		{
			get
			{
				XmlNode xmlNode = this;
				for (;;)
				{
					XmlElement xmlElement = xmlNode as XmlElement;
					if (xmlElement != null && xmlElement.HasAttribute("xml:space"))
					{
						string a = XmlConvert.TrimString(xmlElement.GetAttribute("xml:space"));
						if (a == "default")
						{
							break;
						}
						if (a == "preserve")
						{
							return XmlSpace.Preserve;
						}
					}
					xmlNode = xmlNode.ParentNode;
					if (xmlNode == null)
					{
						return XmlSpace.None;
					}
				}
				return XmlSpace.Default;
			}
		}

		// Token: 0x1700086D RID: 2157
		// (get) Token: 0x06001AFF RID: 6911 RVA: 0x000F00AC File Offset: 0x000EECAC
		internal virtual string XmlLang
		{
			get
			{
				XmlNode xmlNode = this;
				XmlElement xmlElement;
				for (;;)
				{
					xmlElement = (xmlNode as XmlElement);
					if (xmlElement != null && xmlElement.HasAttribute("xml:lang"))
					{
						break;
					}
					xmlNode = xmlNode.ParentNode;
					if (xmlNode == null)
					{
						goto Block_3;
					}
				}
				return xmlElement.GetAttribute("xml:lang");
				Block_3:
				return string.Empty;
			}
		}

		// Token: 0x1700086E RID: 2158
		// (get) Token: 0x06001B00 RID: 6912 RVA: 0x0009F90A File Offset: 0x0009E50A
		internal virtual XPathNodeType XPNodeType
		{
			get
			{
				return (XPathNodeType)(-1);
			}
		}

		// Token: 0x1700086F RID: 2159
		// (get) Token: 0x06001B01 RID: 6913 RVA: 0x00070E99 File Offset: 0x0006FA99
		internal virtual string XPLocalName
		{
			get
			{
				return string.Empty;
			}
		}

		// Token: 0x06001B02 RID: 6914 RVA: 0x00070E99 File Offset: 0x0006FA99
		internal virtual string GetXPAttribute(string localName, string namespaceURI)
		{
			return string.Empty;
		}

		// Token: 0x17000870 RID: 2160
		// (get) Token: 0x06001B03 RID: 6915 RVA: 0x00070238 File Offset: 0x0006EE38
		internal virtual bool IsText
		{
			get
			{
				return false;
			}
		}

		// Token: 0x17000871 RID: 2161
		// (get) Token: 0x06001B04 RID: 6916 RVA: 0x0007140A File Offset: 0x0007000A
		[Nullable(2)]
		public virtual XmlNode PreviousText
		{
			[NullableContext(2)]
			get
			{
				return null;
			}
		}

		// Token: 0x06001B05 RID: 6917 RVA: 0x000F00EF File Offset: 0x000EECEF
		internal static void NestTextNodes(XmlNode prevNode, XmlNode nextNode)
		{
			nextNode.parentNode = prevNode;
		}

		// Token: 0x06001B06 RID: 6918 RVA: 0x000F00F8 File Offset: 0x000EECF8
		internal static void UnnestTextNodes(XmlNode prevNode, XmlNode nextNode)
		{
			nextNode.parentNode = prevNode.ParentNode;
		}

		// Token: 0x17000872 RID: 2162
		// (get) Token: 0x06001B07 RID: 6919 RVA: 0x000F0106 File Offset: 0x000EED06
		private object debuggerDisplayProxy
		{
			get
			{
				return new XmlNode.DebuggerDisplayXmlNodeProxy(this);
			}
		}

		// Token: 0x04000DC3 RID: 3523
		internal XmlNode parentNode;

		// Token: 0x02000265 RID: 613
		[DebuggerDisplay("{ToString()}")]
		internal readonly struct DebuggerDisplayXmlNodeProxy
		{
			// Token: 0x06001B08 RID: 6920 RVA: 0x000F0113 File Offset: 0x000EED13
			public DebuggerDisplayXmlNodeProxy(XmlNode node)
			{
				this._node = node;
			}

			// Token: 0x06001B09 RID: 6921 RVA: 0x000F011C File Offset: 0x000EED1C
			public override string ToString()
			{
				XmlNodeType nodeType = this._node.NodeType;
				string text = nodeType.ToString();
				switch (nodeType)
				{
				case XmlNodeType.Element:
				case XmlNodeType.EntityReference:
					text = text + ", Name=\"" + this._node.Name + "\"";
					break;
				case XmlNodeType.Attribute:
				case XmlNodeType.ProcessingInstruction:
					text = string.Concat(new string[]
					{
						text,
						", Name=\"",
						this._node.Name,
						"\", Value=\"",
						XmlConvert.EscapeValueForDebuggerDisplay(this._node.Value),
						"\""
					});
					break;
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.Comment:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
				case XmlNodeType.XmlDeclaration:
					text = text + ", Value=\"" + XmlConvert.EscapeValueForDebuggerDisplay(this._node.Value) + "\"";
					break;
				case XmlNodeType.DocumentType:
				{
					XmlDocumentType xmlDocumentType = (XmlDocumentType)this._node;
					text = string.Concat(new string[]
					{
						text,
						", Name=\"",
						xmlDocumentType.Name,
						"\", SYSTEM=\"",
						xmlDocumentType.SystemId,
						"\", PUBLIC=\"",
						xmlDocumentType.PublicId,
						"\", Value=\"",
						XmlConvert.EscapeValueForDebuggerDisplay(xmlDocumentType.InternalSubset),
						"\""
					});
					break;
				}
				}
				return text;
			}

			// Token: 0x04000DC4 RID: 3524
			private readonly XmlNode _node;
		}
	}
}
