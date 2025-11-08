using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Xsl;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace TaleWorlds.ObjectSystem
{
	// Token: 0x02000008 RID: 8
	public sealed class MBObjectManager
	{
		// Token: 0x17000008 RID: 8
		// (get) Token: 0x06000037 RID: 55 RVA: 0x00002398 File Offset: 0x00000598
		// (set) Token: 0x06000038 RID: 56 RVA: 0x0000239F File Offset: 0x0000059F
		public static MBObjectManager Instance { get; private set; }

		// Token: 0x06000039 RID: 57 RVA: 0x000023A7 File Offset: 0x000005A7
		private MBObjectManager()
		{
		}

		// Token: 0x0600003A RID: 58 RVA: 0x000023BA File Offset: 0x000005BA
		public static MBObjectManager Init()
		{
			MBObjectManager instance = MBObjectManager.Instance;
			MBObjectManager.Instance = new MBObjectManager();
			return MBObjectManager.Instance;
		}

		// Token: 0x0600003B RID: 59 RVA: 0x000023D1 File Offset: 0x000005D1
		public void Destroy()
		{
			this.ClearAllObjects();
			MBObjectManager.Instance = null;
		}

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x0600003C RID: 60 RVA: 0x000023DF File Offset: 0x000005DF
		public int NumRegisteredTypes
		{
			get
			{
				if (this.ObjectTypeRecords == null)
				{
					return 0;
				}
				return this.ObjectTypeRecords.Count;
			}
		}

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x0600003D RID: 61 RVA: 0x000023F6 File Offset: 0x000005F6
		public int MaxRegisteredTypes
		{
			get
			{
				return 256;
			}
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00002400 File Offset: 0x00000600
		public void RegisterType<T>(string classPrefix, string classListPrefix, uint typeId, bool autoCreateInstance = true, bool isTemporary = false) where T : MBObjectBase
		{
			if (this.NumRegisteredTypes > this.MaxRegisteredTypes)
			{
				Debug.FailedAssert(new MBTooManyRegisteredTypesException().ToString(), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "RegisterType", 64);
			}
			this.ObjectTypeRecords.Add(new MBObjectManager.ObjectTypeRecord<T>(typeId, classPrefix, classListPrefix, autoCreateInstance, isTemporary));
		}

		// Token: 0x0600003F RID: 63 RVA: 0x0000244D File Offset: 0x0000064D
		public bool HasType<T>() where T : MBObjectBase
		{
			return this.HasTypeInternal(typeof(T));
		}

		// Token: 0x06000040 RID: 64 RVA: 0x0000245F File Offset: 0x0000065F
		public bool HasType(Type type)
		{
			return this.HasTypeInternal(type);
		}

		// Token: 0x06000041 RID: 65 RVA: 0x00002468 File Offset: 0x00000668
		private bool HasTypeInternal(Type type)
		{
			if (type.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						if (enumerator.Current.ObjectClass == type)
						{
							return true;
						}
					}
					return false;
				}
			}
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (type.IsAssignableFrom(objectTypeRecord.ObjectClass))
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000042 RID: 66 RVA: 0x0000251C File Offset: 0x0000071C
		public string FindRegisteredClassPrefix(Type type)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ObjectClass == type)
				{
					return objectTypeRecord.ElementName;
				}
			}
			Debug.FailedAssert(type.Name + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "FindRegisteredClassPrefix", 116);
			return null;
		}

		// Token: 0x06000043 RID: 67 RVA: 0x000025A4 File Offset: 0x000007A4
		public Type FindRegisteredType(string classPrefix)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ElementName == classPrefix)
				{
					return objectTypeRecord.ObjectClass;
				}
			}
			Debug.FailedAssert(classPrefix + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "FindRegisteredType", 130);
			return null;
		}

		// Token: 0x06000044 RID: 68 RVA: 0x0000262C File Offset: 0x0000082C
		public T RegisterObject<T>(T obj) where T : MBObjectBase
		{
			MBObjectBase mbobjectBase;
			this.RegisterObjectInternalWithoutTypeId<T>(obj, false, out mbobjectBase);
			return mbobjectBase as T;
		}

		// Token: 0x06000045 RID: 69 RVA: 0x00002650 File Offset: 0x00000850
		public T RegisterPresumedObject<T>(T obj) where T : MBObjectBase
		{
			MBObjectBase mbobjectBase;
			this.RegisterObjectInternalWithoutTypeId<T>(obj, true, out mbobjectBase);
			return mbobjectBase as T;
		}

		// Token: 0x06000046 RID: 70 RVA: 0x00002674 File Offset: 0x00000874
		internal void TryRegisterObjectWithoutInitialization(MBObjectBase obj)
		{
			Type type = obj.GetType();
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ObjectClass == type)
				{
					objectTypeRecord.RegisterMBObjectWithoutInitialization(obj);
					return;
				}
			}
			Debug.FailedAssert(obj.GetType().Name + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "TryRegisterObjectWithoutInitialization", 161);
		}

		// Token: 0x06000047 RID: 71 RVA: 0x00002708 File Offset: 0x00000908
		private void RegisterObjectInternalWithoutTypeId<T>(T obj, bool presumed, out MBObjectBase registeredObject) where T : MBObjectBase
		{
			Type right = obj.GetType();
			right = typeof(T);
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ObjectClass == right)
				{
					objectTypeRecord.RegisterMBObject(obj, presumed, out registeredObject);
					return;
				}
			}
			registeredObject = null;
			Debug.FailedAssert(typeof(T).Name + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "RegisterObjectInternalWithoutTypeId", 178);
		}

		// Token: 0x06000048 RID: 72 RVA: 0x000027BC File Offset: 0x000009BC
		public void UnregisterObject(MBObjectBase obj)
		{
			if (obj == null)
			{
				return;
			}
			Type type = obj.GetType();
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (type == objectTypeRecord.ObjectClass)
				{
					objectTypeRecord.UnregisterMBObject(obj);
					this.AfterUnregisterObject(obj);
					return;
				}
			}
			Debug.FailedAssert("UnregisterObject call for an unregistered object! Type: " + obj.GetType(), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "UnregisterObject", 200);
		}

		// Token: 0x06000049 RID: 73 RVA: 0x00002858 File Offset: 0x00000A58
		private void AfterUnregisterObject(MBObjectBase obj)
		{
			if (this._handlers != null)
			{
				foreach (IObjectManagerHandler objectManagerHandler in this._handlers)
				{
					objectManagerHandler.AfterUnregisterObject(obj);
				}
			}
		}

		// Token: 0x0600004A RID: 74 RVA: 0x000028B4 File Offset: 0x00000AB4
		public T GetObject<T>(Func<T, bool> predicate) where T : MBObjectBase
		{
			Type typeFromHandle = typeof(T);
			if (typeFromHandle.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						MBObjectManager.IObjectTypeRecord objectTypeRecord = enumerator.Current;
						if (objectTypeRecord.ObjectClass == typeFromHandle)
						{
							return ((MBObjectManager.ObjectTypeRecord<T>)objectTypeRecord).FirstOrDefault(predicate);
						}
					}
					goto IL_AE;
				}
			}
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord2 in this.ObjectTypeRecords)
			{
				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass))
				{
					return objectTypeRecord2.OfType<T>().FirstOrDefault(predicate);
				}
			}
			IL_AE:
			return default(T);
		}

		// Token: 0x0600004B RID: 75 RVA: 0x00002998 File Offset: 0x00000B98
		public MBReadOnlyList<T> GetObjects<T>(Func<T, bool> predicate) where T : MBObjectBase
		{
			MBList<T> mblist = new MBList<T>();
			Type typeFromHandle = typeof(T);
			if (typeFromHandle.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						MBObjectManager.IObjectTypeRecord objectTypeRecord = enumerator.Current;
						if (objectTypeRecord.ObjectClass == typeFromHandle)
						{
							MBObjectManager.ObjectTypeRecord<T> source = (MBObjectManager.ObjectTypeRecord<T>)objectTypeRecord;
							mblist.AddRange(source.Where(predicate));
						}
					}
					return mblist;
				}
			}
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord2 in this.ObjectTypeRecords)
			{
				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass))
				{
					mblist.AddRange(objectTypeRecord2.OfType<T>().Where(predicate));
				}
			}
			return mblist;
		}

		// Token: 0x0600004C RID: 76 RVA: 0x00002A80 File Offset: 0x00000C80
		public T GetObject<T>(string objectName) where T : MBObjectBase
		{
			Type typeFromHandle = typeof(T);
			if (typeFromHandle.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						MBObjectManager.IObjectTypeRecord objectTypeRecord = enumerator.Current;
						if (objectTypeRecord.ObjectClass == typeFromHandle)
						{
							return ((MBObjectManager.ObjectTypeRecord<T>)objectTypeRecord).GetObject(objectName);
						}
					}
					goto IL_C3;
				}
			}
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord2 in this.ObjectTypeRecords)
			{
				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass))
				{
					T t = objectTypeRecord2.GetMBObject(objectName) as T;
					if (t != null)
					{
						return t;
					}
				}
			}
			IL_C3:
			return default(T);
		}

		// Token: 0x0600004D RID: 77 RVA: 0x00002B78 File Offset: 0x00000D78
		public T GetFirstObject<T>() where T : MBObjectBase
		{
			Type typeFromHandle = typeof(T);
			if (typeFromHandle.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						MBObjectManager.IObjectTypeRecord objectTypeRecord = enumerator.Current;
						if (objectTypeRecord.ObjectClass == typeFromHandle)
						{
							return ((MBObjectManager.ObjectTypeRecord<T>)objectTypeRecord).GetFirstObject();
						}
					}
					goto IL_C0;
				}
			}
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord2 in this.ObjectTypeRecords)
			{
				T result;
				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass) && (result = (objectTypeRecord2.GetFirstMBObject() as T)) != null)
				{
					return result;
				}
			}
			IL_C0:
			return default(T);
		}

		// Token: 0x0600004E RID: 78 RVA: 0x00002C70 File Offset: 0x00000E70
		public bool ContainsObject<T>(string objectName) where T : MBObjectBase
		{
			Type typeFromHandle = typeof(T);
			if (typeFromHandle.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						MBObjectManager.IObjectTypeRecord objectTypeRecord = enumerator.Current;
						if (objectTypeRecord.ObjectClass == typeFromHandle)
						{
							return objectTypeRecord.ContainsObject(objectName);
						}
					}
					return false;
				}
			}
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord2 in this.ObjectTypeRecords)
			{
				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass))
				{
					bool flag = objectTypeRecord2.ContainsObject(objectName);
					if (flag)
					{
						return flag;
					}
				}
			}
			return false;
		}

		// Token: 0x0600004F RID: 79 RVA: 0x00002D48 File Offset: 0x00000F48
		public void RemoveTemporaryTypes()
		{
			for (int i = this.ObjectTypeRecords.Count - 1; i >= 0; i--)
			{
				MBObjectManager.IObjectTypeRecord objectTypeRecord = this.ObjectTypeRecords[i];
				if (objectTypeRecord.IsTemporary)
				{
					foreach (object obj in objectTypeRecord)
					{
						MBObjectBase obj2 = (MBObjectBase)obj;
						this.UnregisterObject(obj2);
					}
					this.ObjectTypeRecords.Remove(objectTypeRecord);
				}
			}
		}

		// Token: 0x06000050 RID: 80 RVA: 0x00002DDC File Offset: 0x00000FDC
		public void PreAfterLoad()
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				objectTypeRecord.PreAfterLoad();
			}
		}

		// Token: 0x06000051 RID: 81 RVA: 0x00002E2C File Offset: 0x0000102C
		public void AfterLoad()
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				objectTypeRecord.AfterLoad();
			}
		}

		// Token: 0x06000052 RID: 82 RVA: 0x00002E7C File Offset: 0x0000107C
		public MBObjectBase GetObject(MBGUID objectId)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.TypeNo == objectId.GetTypeIndex())
				{
					return objectTypeRecord.GetMBObject(objectId);
				}
			}
			Debug.FailedAssert(objectId.GetTypeIndex() + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "GetObject", 424);
			return null;
		}

		// Token: 0x06000053 RID: 83 RVA: 0x00002F10 File Offset: 0x00001110
		public MBObjectBase GetObject(string typeName, string objectName)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ElementName == typeName)
				{
					return objectTypeRecord.GetMBObject(objectName);
				}
			}
			Debug.FailedAssert(typeName + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "GetObject", 439);
			return null;
		}

		// Token: 0x06000054 RID: 84 RVA: 0x00002F98 File Offset: 0x00001198
		private MBObjectBase GetPresumedObject(string typeName, string objectName, bool isInitialize = false)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ElementName == typeName)
				{
					MBObjectBase mbobjectBase = objectTypeRecord.GetMBObject(objectName);
					if (mbobjectBase != null)
					{
						return mbobjectBase;
					}
					if (objectTypeRecord.AutoCreate)
					{
						mbobjectBase = objectTypeRecord.CreatePresumedMBObject(objectName);
						MBObjectBase result;
						objectTypeRecord.RegisterMBObject(mbobjectBase, true, out result);
						return result;
					}
					throw new MBCanNotCreatePresumedObjectException();
				}
			}
			Debug.FailedAssert(typeName + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "GetPresumedObject", 467);
			return null;
		}

		// Token: 0x06000055 RID: 85 RVA: 0x00003048 File Offset: 0x00001248
		public MBReadOnlyList<T> GetObjectTypeList<T>() where T : MBObjectBase
		{
			Type typeFromHandle = typeof(T);
			if (typeFromHandle.IsSealed)
			{
				using (List<MBObjectManager.IObjectTypeRecord>.Enumerator enumerator = this.ObjectTypeRecords.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						MBObjectManager.IObjectTypeRecord objectTypeRecord = enumerator.Current;
						if (objectTypeRecord.ObjectClass == typeFromHandle)
						{
							return ((MBObjectManager.ObjectTypeRecord<T>)objectTypeRecord).GetObjectsList();
						}
					}
					goto IL_F4;
				}
				goto IL_64;
				IL_F4:
				Debug.FailedAssert(typeof(T).Name + " could not be found in MBObjectManager objectTypeRecords!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.ObjectSystem\\MBObjectManager.cs", "GetObjectTypeList", 504);
				return null;
			}
			IL_64:
			MBList<T> mblist = new MBList<T>();
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord2 in this.ObjectTypeRecords)
			{
				if (typeFromHandle.IsAssignableFrom(objectTypeRecord2.ObjectClass))
				{
					foreach (object obj in objectTypeRecord2.GetList())
					{
						mblist.Add((T)((object)obj));
					}
				}
			}
			return mblist;
		}

		// Token: 0x06000056 RID: 86 RVA: 0x000031A4 File Offset: 0x000013A4
		public IList<MBObjectBase> CreateObjectTypeList(Type objectClassType)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ObjectClass == objectClassType)
				{
					List<MBObjectBase> list = new List<MBObjectBase>();
					foreach (object obj in objectTypeRecord)
					{
						MBObjectBase item = obj as MBObjectBase;
						list.Add(item);
					}
					return list;
				}
			}
			return null;
		}

		// Token: 0x06000057 RID: 87 RVA: 0x00003258 File Offset: 0x00001458
		public void LoadXML(string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor = false)
		{
			bool ignoreGameTypeInclusionCheck = skipXmlFilterForEditor || isDevelopment;
			XmlDocument mergedXmlForManaged = MBObjectManager.GetMergedXmlForManaged(id, false, ignoreGameTypeInclusionCheck, gameType);
			try
			{
				this.LoadXml(mergedXmlForManaged, isDevelopment);
			}
			catch (Exception)
			{
			}
		}

		// Token: 0x06000058 RID: 88 RVA: 0x00003294 File Offset: 0x00001494
		public static bool MergeElementAttributes(XElement element1, XElement element2)
		{
			if (element1 != null && element2 != null)
			{
				IEnumerable<XAttribute> enumerable = element2.Attributes();
				bool flag = enumerable.Any((XAttribute a) => a.Name.LocalName == "_replaceWhileMerging" && a.Value == "true");
				if (flag)
				{
					element1.RemoveAttributes();
				}
				foreach (XAttribute xattribute in enumerable)
				{
					element1.SetAttributeValue(xattribute.Name, xattribute.Value);
				}
				return flag;
			}
			return false;
		}

		// Token: 0x06000059 RID: 89 RVA: 0x00003324 File Offset: 0x00001524
		public static void MergeElements(XElement element1, XElement element2, string xsdPath)
		{
			bool flag = MBObjectManager.MergeElementAttributes(element1, element2);
			if (element1.Value != "" && element2.Value != "")
			{
				element1.Value = element2.Value;
			}
			Dictionary<string, XmlResource.XsdElement> elementSchema = XmlResource.XsdElementDictionary[xsdPath];
			IEnumerable<XElement> enumerable = element2.Elements() ?? Enumerable.Empty<XElement>();
			if (flag)
			{
				element1.Elements().Remove<XElement>();
			}
			Dictionary<XName, Dictionary<string, XElement>> dictionary = (from element in element1.Elements()
			group element by element.Name).ToDictionary((IGrouping<XName, XElement> el) => el.Key, delegate(IGrouping<XName, XElement> el)
			{
				XElement element4 = el.First<XElement>();
				List<string> uniqueAttributes = elementSchema[XmlResource.GetFullXPathOfElement(element4, false)].UniqueAttributes;
				Dictionary<string, XElement> dictionary3 = new Dictionary<string, XElement>();
				using (IEnumerator<XElement> enumerator2 = el.GetEnumerator())
				{
					while (enumerator2.MoveNext())
					{
						XElement element = enumerator2.Current;
						string key = string.Concat(uniqueAttributes.Select(delegate(string attribute)
						{
							XElement element5 = element;
							string text2;
							if (element5 == null)
							{
								text2 = null;
							}
							else
							{
								XAttribute xattribute = element5.Attribute(attribute);
								text2 = ((xattribute != null) ? xattribute.Value : null);
							}
							return text2 ?? string.Empty;
						}));
						dictionary3[key] = element;
					}
				}
				return dictionary3;
			});
			using (IEnumerator<XElement> enumerator = enumerable.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					XElement element2Element = enumerator.Current;
					Dictionary<string, XElement> dictionary2;
					if (dictionary.TryGetValue(element2Element.Name, out dictionary2))
					{
						XElement value = dictionary2.First<KeyValuePair<string, XElement>>().Value;
						if (elementSchema[XmlResource.GetFullXPathOfElement(value, false)].AlwaysPreferMerge)
						{
							MBObjectManager.MergeElements(value, element2Element, xsdPath);
						}
						else
						{
							string text = string.Concat(elementSchema[XmlResource.GetFullXPathOfElement(dictionary2.First<KeyValuePair<string, XElement>>().Value, false)].UniqueAttributes.Select(delegate(string attribute)
							{
								XElement element2Element = element2Element;
								string text2;
								if (element2Element == null)
								{
									text2 = null;
								}
								else
								{
									XAttribute xattribute = element2Element.Attribute(attribute);
									text2 = ((xattribute != null) ? xattribute.Value : null);
								}
								return text2 ?? string.Empty;
							}));
							XElement element3;
							if (dictionary2.TryGetValue(text, out element3) && text != "")
							{
								MBObjectManager.MergeElements(element3, element2Element, xsdPath);
							}
							else if (element2Element.Attributes().Any((XAttribute a) => a.Name.LocalName == "_replaceWhileMerging" && a.Value == "true"))
							{
								MBObjectManager.MergeElements(value, element2Element, xsdPath);
							}
							else
							{
								element1.Add(element2Element);
							}
						}
					}
					else
					{
						element1.Add(element2Element);
					}
				}
			}
		}

		// Token: 0x0600005A RID: 90 RVA: 0x00003570 File Offset: 0x00001770
		public static XmlDocument GetMergedXmlForManaged(string id, bool skipValidation, bool ignoreGameTypeInclusionCheck = true, string gameType = "")
		{
			List<Tuple<string, string>> list = new List<Tuple<string, string>>();
			List<string> xsltList = new List<string>();
			string xsdPath = ModuleHelper.GetXsdPath(id);
			foreach (MbObjectXmlInformation mbObjectXmlInformation in XmlResource.XmlInformationList)
			{
				if (mbObjectXmlInformation.Id == id && ModuleHelper.IsModuleActive(mbObjectXmlInformation.ModuleName) && (ignoreGameTypeInclusionCheck || mbObjectXmlInformation.GameTypesIncluded.Count == 0 || mbObjectXmlInformation.GameTypesIncluded.Contains(gameType)))
				{
					string text = ModuleHelper.GetXsdPathForModules(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Id);
					if (!File.Exists(text))
					{
						text = xsdPath;
					}
					string text2 = ModuleHelper.GetXmlPath(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name);
					if (File.Exists(text2))
					{
						list.Add(Tuple.Create<string, string>(ModuleHelper.GetXmlPath(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name), text));
						MBObjectManager.HandleXsltList(ModuleHelper.GetXsltPath(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name), ref xsltList);
					}
					else
					{
						string text3 = text2.Replace(".xml", "");
						if (Directory.Exists(text3))
						{
							foreach (FileInfo fileInfo in new DirectoryInfo(text3).GetFiles("*.xml"))
							{
								text2 = text3 + "/" + fileInfo.Name;
								list.Add(Tuple.Create<string, string>(text2, text));
								MBObjectManager.HandleXsltList(text2.Replace(".xml", ".xsl"), ref xsltList);
							}
						}
						else
						{
							list.Add(Tuple.Create<string, string>("", ""));
							MBObjectManager.HandleXsltList(ModuleHelper.GetXsltPath(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name), ref xsltList);
						}
					}
				}
			}
			return MBObjectManager.CreateMergedXmlFile(list, xsltList, skipValidation);
		}

		// Token: 0x0600005B RID: 91 RVA: 0x00003768 File Offset: 0x00001968
		public static XmlDocument GetMergedXmlForNative(string id, out List<string> usedPaths)
		{
			usedPaths = new List<string>();
			List<Tuple<string, string>> list = new List<Tuple<string, string>>();
			List<string> xsltList = new List<string>();
			string text = ModuleHelper.GetXsdPath(id) ?? string.Empty;
			if (!File.Exists(text))
			{
				text = "";
			}
			foreach (MbObjectXmlInformation mbObjectXmlInformation in XmlResource.MbprojXmls)
			{
				if (mbObjectXmlInformation.Id == id)
				{
					if (File.Exists(ModuleHelper.GetXmlPathForNative(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name)))
					{
						usedPaths.Add(ModuleHelper.GetXmlPathForNativeWBase(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name));
						list.Add(Tuple.Create<string, string>(ModuleHelper.GetXmlPathForNative(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name), text));
					}
					else
					{
						list.Add(Tuple.Create<string, string>("", ""));
					}
					MBObjectManager.HandleXsltList(ModuleHelper.GetXsltPathForNative(mbObjectXmlInformation.ModuleName, mbObjectXmlInformation.Name), ref xsltList);
				}
			}
			return MBObjectManager.CreateMergedXmlFile(list, xsltList, true);
		}

		// Token: 0x0600005C RID: 92 RVA: 0x0000388C File Offset: 0x00001A8C
		private static bool HandleXsltList(string xslPath, ref List<string> xsltList)
		{
			string text = xslPath + "t";
			if (File.Exists(xslPath))
			{
				xsltList.Add(xslPath);
				return true;
			}
			if (File.Exists(text))
			{
				xsltList.Add(text);
				return true;
			}
			xsltList.Add("");
			return false;
		}

		// Token: 0x0600005D RID: 93 RVA: 0x000038D8 File Offset: 0x00001AD8
		public static XmlDocument CreateMergedXmlFile(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)
		{
			XmlDocument xmlDocument = MBObjectManager.CreateDocumentFromXmlFile(toBeMerged[0].Item1, toBeMerged[0].Item2, skipValidation);
			for (int i = 1; i < toBeMerged.Count; i++)
			{
				if (xsltList[i] != "")
				{
					xmlDocument = MBObjectManager.ApplyXslt(xsltList[i], xmlDocument);
				}
				if (toBeMerged[i].Item1 != "")
				{
					XmlDocument xmlDocument2 = MBObjectManager.CreateDocumentFromXmlFile(toBeMerged[i].Item1, toBeMerged[i].Item2, skipValidation);
					xmlDocument = MBObjectManager.MergeTwoXmls(xmlDocument, xmlDocument2, toBeMerged[i].Item2, false);
				}
			}
			return xmlDocument;
		}

		// Token: 0x0600005E RID: 94 RVA: 0x00003984 File Offset: 0x00001B84
		public static XmlDocument ApplyXslt(string xsltPath, XmlDocument baseDocument)
		{
			XmlReader input = new XmlNodeReader(baseDocument);
			XmlReader stylesheet = XmlReader.Create(new StreamReader(xsltPath));
			XslCompiledTransform xslCompiledTransform = new XslCompiledTransform();
			xslCompiledTransform.Load(stylesheet);
			XmlDocument xmlDocument = new XmlDocument(baseDocument.CreateNavigator().NameTable);
			using (XmlWriter xmlWriter = xmlDocument.CreateNavigator().AppendChild())
			{
				xslCompiledTransform.Transform(input, xmlWriter);
				xmlWriter.Close();
			}
			return xmlDocument;
		}

		// Token: 0x0600005F RID: 95 RVA: 0x00003A00 File Offset: 0x00001C00
		public static XmlDocument MergeTwoXmls(XmlDocument xmlDocument1, XmlDocument xmlDocument2, string xsdPath, bool keepDuplicates)
		{
			XDocument xdocument = MBObjectManager.ToXDocument(xmlDocument1);
			XDocument xdocument2 = MBObjectManager.ToXDocument(xmlDocument2);
			if (keepDuplicates || xsdPath == "")
			{
				xdocument.Root.Add(xdocument2.Root.Elements());
			}
			else
			{
				MBObjectManager.MergeElements(xdocument.Root, xdocument2.Root, xsdPath);
			}
			return MBObjectManager.ToXmlDocument(xdocument);
		}

		// Token: 0x06000060 RID: 96 RVA: 0x00003A5C File Offset: 0x00001C5C
		public static XDocument ToXDocument(XmlDocument xmlDocument)
		{
			XDocument result;
			using (XmlNodeReader xmlNodeReader = new XmlNodeReader(xmlDocument))
			{
				try
				{
					xmlNodeReader.MoveToContent();
					result = XDocument.Load(xmlNodeReader);
				}
				catch (Exception ex)
				{
					Debug.Print(ex.Message, 0, Debug.DebugColor.White, 17592186044416UL);
					throw;
				}
			}
			return result;
		}

		// Token: 0x06000061 RID: 97 RVA: 0x00003AC0 File Offset: 0x00001CC0
		public static XmlDocument ToXmlDocument(XDocument xDocument)
		{
			XmlDocument xmlDocument = new XmlDocument();
			using (xDocument.CreateReader())
			{
				xmlDocument.Load(xDocument.CreateReader());
			}
			return xmlDocument;
		}

		// Token: 0x06000062 RID: 98 RVA: 0x00003B04 File Offset: 0x00001D04
		public void LoadOneXmlFromFile(string xmlPath, string xsdPath, bool skipValidation = false)
		{
			try
			{
				XmlDocument doc = MBObjectManager.CreateDocumentFromXmlFile(xmlPath, xsdPath, skipValidation);
				this.LoadXml(doc, false);
			}
			catch (Exception)
			{
			}
		}

		// Token: 0x06000063 RID: 99 RVA: 0x00003B38 File Offset: 0x00001D38
		public XmlDocument LoadXMLFromFileSkipValidation(string xmlPath, string xsdPath)
		{
			XmlDocument result;
			try
			{
				result = MBObjectManager.CreateDocumentFromXmlFile(xmlPath, xsdPath, true);
			}
			catch
			{
				result = null;
			}
			return result;
		}

		// Token: 0x06000064 RID: 100 RVA: 0x00003B68 File Offset: 0x00001D68
		private static void LoadXmlWithValidation(string xmlPath, string xsdPath, XmlDocument xmlDocument)
		{
			Debug.Print("opening " + xsdPath, 0, Debug.DebugColor.White, 17592186044416UL);
			XmlSchemaSet xmlSchemaSet = new XmlSchemaSet();
			XmlTextReader xmlTextReader = null;
			try
			{
				xmlTextReader = new XmlTextReader(new StreamReader(xsdPath));
				xmlSchemaSet.Add(null, xmlTextReader);
			}
			catch (FileNotFoundException)
			{
				Debug.Print("xsd file of " + xmlPath + " could not be found!", 0, Debug.DebugColor.Red, 17592186044416UL);
			}
			catch (XmlSchemaException ex)
			{
				Debug.Print(string.Concat(new object[]
				{
					"XmlSchemaException, line number: ",
					ex.LineNumber,
					", line position: ",
					ex.LinePosition,
					", SourceSchemaObject: ",
					ex.SourceSchemaObject
				}), 0, Debug.DebugColor.White, 17592186044416UL);
				Debug.Print("xsd file of " + xmlPath + " could not be read! " + ex.Message, 0, Debug.DebugColor.Red, 17592186044416UL);
			}
			catch (ArgumentNullException ex2)
			{
				Debug.Print("ArgumentNullException, ParamName: " + ex2.ParamName, 0, Debug.DebugColor.White, 17592186044416UL);
				Debug.Print("xsd file of " + xmlPath + " could not be read! " + ex2.Message, 0, Debug.DebugColor.Red, 17592186044416UL);
			}
			catch (Exception ex3)
			{
				Debug.Print("xsd file of " + xmlPath + " could not be read! " + ex3.Message, 0, Debug.DebugColor.Red, 17592186044416UL);
			}
			try
			{
				xmlSchemaSet.Compile();
				MBObjectManager.InjectOptionalAttrToAllComplexTypes(xmlSchemaSet, "boolean", "_replaceWhileMerging");
				foreach (object obj in xmlSchemaSet.Schemas())
				{
					XmlSchema schema = (XmlSchema)obj;
					xmlSchemaSet.Reprocess(schema);
				}
				xmlSchemaSet.Compile();
			}
			catch (Exception ex4)
			{
				Debug.Print("Schema overlay failed: " + ex4.Message, 0, Debug.DebugColor.Red, 17592186044416UL);
			}
			XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
			xmlReaderSettings.ValidationType = ValidationType.None;
			xmlReaderSettings.Schemas.Add(xmlSchemaSet);
			xmlReaderSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
			xmlReaderSettings.ValidationEventHandler += MBObjectManager.ValidationEventHandler;
			xmlReaderSettings.CloseInput = true;
			try
			{
				XmlReader xmlReader = XmlReader.Create(new StreamReader(xmlPath), xmlReaderSettings);
				xmlDocument.Load(xmlReader);
				xmlReader.Close();
				XmlReaderSettings xmlReaderSettings2 = new XmlReaderSettings();
				xmlReaderSettings2.ValidationType = ValidationType.Schema;
				xmlReaderSettings2.Schemas.Add(xmlSchemaSet);
				xmlReaderSettings2.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
				xmlReaderSettings2.ValidationEventHandler += MBObjectManager.ValidationEventHandler;
				xmlReaderSettings2.CloseInput = true;
				xmlReader = XmlReader.Create(new StreamReader(xmlPath), xmlReaderSettings2);
				xmlDocument.Load(xmlReader);
				xmlReader.Close();
			}
			catch (Exception)
			{
				string localPath = new Uri(xmlDocument.BaseURI).LocalPath;
			}
			if (xmlTextReader != null)
			{
				xmlTextReader.Close();
			}
		}

		// Token: 0x06000065 RID: 101 RVA: 0x00003E98 File Offset: 0x00002098
		private static bool HasAttibuteAux(XmlSchemaAttribute schemaAttribute, string localName)
		{
			return (!string.IsNullOrEmpty(schemaAttribute.Name) && schemaAttribute.Name == localName) || (!schemaAttribute.RefName.IsEmpty && schemaAttribute.RefName.Name == localName);
		}

		// Token: 0x06000066 RID: 102 RVA: 0x00003ED8 File Offset: 0x000020D8
		private static bool HasAttribute(XmlSchemaComplexType complexType, string localName)
		{
			XmlSchemaComplexContent xmlSchemaComplexContent;
			if ((xmlSchemaComplexContent = (complexType.ContentModel as XmlSchemaComplexContent)) != null)
			{
				XmlSchemaComplexContentExtension xmlSchemaComplexContentExtension;
				if ((xmlSchemaComplexContentExtension = (xmlSchemaComplexContent.Content as XmlSchemaComplexContentExtension)) != null)
				{
					using (XmlSchemaObjectEnumerator enumerator = xmlSchemaComplexContentExtension.Attributes.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							XmlSchemaAttribute schemaAttribute;
							if ((schemaAttribute = (enumerator.Current as XmlSchemaAttribute)) != null && MBObjectManager.HasAttibuteAux(schemaAttribute, localName))
							{
								return true;
							}
						}
						goto IL_1A5;
					}
				}
				XmlSchemaComplexContentRestriction xmlSchemaComplexContentRestriction;
				if ((xmlSchemaComplexContentRestriction = (xmlSchemaComplexContent.Content as XmlSchemaComplexContentRestriction)) == null)
				{
					goto IL_1A5;
				}
				using (XmlSchemaObjectEnumerator enumerator = xmlSchemaComplexContentRestriction.Attributes.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						XmlSchemaAttribute schemaAttribute2;
						if ((schemaAttribute2 = (enumerator.Current as XmlSchemaAttribute)) != null && MBObjectManager.HasAttibuteAux(schemaAttribute2, localName))
						{
							return true;
						}
					}
					goto IL_1A5;
				}
			}
			XmlSchemaSimpleContent xmlSchemaSimpleContent;
			if ((xmlSchemaSimpleContent = (complexType.ContentModel as XmlSchemaSimpleContent)) != null)
			{
				XmlSchemaSimpleContentExtension xmlSchemaSimpleContentExtension;
				if ((xmlSchemaSimpleContentExtension = (xmlSchemaSimpleContent.Content as XmlSchemaSimpleContentExtension)) != null)
				{
					using (XmlSchemaObjectEnumerator enumerator = xmlSchemaSimpleContentExtension.Attributes.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							XmlSchemaAttribute schemaAttribute3;
							if ((schemaAttribute3 = (enumerator.Current as XmlSchemaAttribute)) != null && MBObjectManager.HasAttibuteAux(schemaAttribute3, localName))
							{
								return true;
							}
						}
						goto IL_1A5;
					}
				}
				XmlSchemaSimpleContentRestriction xmlSchemaSimpleContentRestriction;
				if ((xmlSchemaSimpleContentRestriction = (xmlSchemaSimpleContent.Content as XmlSchemaSimpleContentRestriction)) != null)
				{
					using (XmlSchemaObjectEnumerator enumerator = xmlSchemaSimpleContentRestriction.Attributes.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							XmlSchemaAttribute schemaAttribute4;
							if ((schemaAttribute4 = (enumerator.Current as XmlSchemaAttribute)) != null && MBObjectManager.HasAttibuteAux(schemaAttribute4, localName))
							{
								return true;
							}
						}
					}
				}
			}
			IL_1A5:
			using (XmlSchemaObjectEnumerator enumerator = complexType.Attributes.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					XmlSchemaAttribute schemaAttribute5;
					if ((schemaAttribute5 = (enumerator.Current as XmlSchemaAttribute)) != null && MBObjectManager.HasAttibuteAux(schemaAttribute5, localName))
					{
						return true;
					}
				}
			}
			return false;
		}

		// Token: 0x06000067 RID: 103 RVA: 0x0000411C File Offset: 0x0000231C
		private static void InjectAttribute(XmlSchemaComplexType complexType, XmlQualifiedName qualifiedName, string attributeName)
		{
			if (!MBObjectManager.HasAttribute(complexType, attributeName))
			{
				XmlSchemaAttribute item = new XmlSchemaAttribute
				{
					Name = attributeName,
					SchemaTypeName = qualifiedName,
					Use = XmlSchemaUse.Optional,
					Form = XmlSchemaForm.Unqualified
				};
				XmlSchemaComplexContent xmlSchemaComplexContent;
				XmlSchemaSimpleContent xmlSchemaSimpleContent;
				if ((xmlSchemaComplexContent = (complexType.ContentModel as XmlSchemaComplexContent)) != null)
				{
					XmlSchemaComplexContentExtension xmlSchemaComplexContentExtension;
					if ((xmlSchemaComplexContentExtension = (xmlSchemaComplexContent.Content as XmlSchemaComplexContentExtension)) != null)
					{
						xmlSchemaComplexContentExtension.Attributes.Add(item);
						return;
					}
					if (!(xmlSchemaComplexContent.Content is XmlSchemaComplexContentRestriction))
					{
						complexType.Attributes.Add(item);
						return;
					}
				}
				else if ((xmlSchemaSimpleContent = (complexType.ContentModel as XmlSchemaSimpleContent)) != null)
				{
					XmlSchemaSimpleContentExtension xmlSchemaSimpleContentExtension;
					if ((xmlSchemaSimpleContentExtension = (xmlSchemaSimpleContent.Content as XmlSchemaSimpleContentExtension)) != null)
					{
						xmlSchemaSimpleContentExtension.Attributes.Add(item);
						return;
					}
					if (!(xmlSchemaSimpleContent.Content is XmlSchemaSimpleContentRestriction))
					{
						complexType.Attributes.Add(item);
						return;
					}
				}
				else
				{
					complexType.Attributes.Add(item);
				}
			}
		}

		// Token: 0x06000068 RID: 104 RVA: 0x000041F4 File Offset: 0x000023F4
		private static void InjectOptionalAttrToAllComplexTypes(XmlSchemaSet set, string type, string name)
		{
			XmlQualifiedName xmlQualifiedName = new XmlQualifiedName(type, "http://www.w3.org/2001/XMLSchema");
			foreach (object obj in set.Schemas())
			{
				foreach (XmlSchemaObject xmlSchemaObject in ((XmlSchema)obj).Items)
				{
					XmlSchemaComplexType xmlSchemaComplexType;
					XmlSchemaElement xmlSchemaElement;
					XmlSchemaGroup xmlSchemaGroup;
					if ((xmlSchemaComplexType = (xmlSchemaObject as XmlSchemaComplexType)) != null)
					{
						MBObjectManager.InjectAttribute(xmlSchemaComplexType, xmlQualifiedName, name);
						MBObjectManager.WalkParticle(xmlSchemaComplexType.Particle, xmlQualifiedName, name);
					}
					else if ((xmlSchemaElement = (xmlSchemaObject as XmlSchemaElement)) != null)
					{
						XmlSchemaComplexType xmlSchemaComplexType2;
						if ((xmlSchemaComplexType2 = (xmlSchemaElement.SchemaType as XmlSchemaComplexType)) != null)
						{
							MBObjectManager.InjectAttribute(xmlSchemaComplexType2, xmlQualifiedName, name);
							MBObjectManager.WalkParticle(xmlSchemaComplexType2.Particle, xmlQualifiedName, name);
						}
						XmlSchemaComplexType xmlSchemaComplexType3;
						if ((xmlSchemaComplexType3 = (xmlSchemaElement.ElementSchemaType as XmlSchemaComplexType)) != null)
						{
							MBObjectManager.InjectAttribute(xmlSchemaComplexType3, xmlQualifiedName, name);
							MBObjectManager.WalkParticle(xmlSchemaComplexType3.Particle, xmlQualifiedName, name);
						}
					}
					else if ((xmlSchemaGroup = (xmlSchemaObject as XmlSchemaGroup)) != null)
					{
						MBObjectManager.WalkParticle(xmlSchemaGroup.Particle, xmlQualifiedName, name);
					}
				}
			}
		}

		// Token: 0x06000069 RID: 105 RVA: 0x00004338 File Offset: 0x00002538
		private static void WalkParticle(XmlSchemaParticle particle, XmlQualifiedName xsType, string name)
		{
			if (particle != null)
			{
				XmlSchemaElement xmlSchemaElement;
				if ((xmlSchemaElement = (particle as XmlSchemaElement)) != null)
				{
					XmlSchemaComplexType xmlSchemaComplexType;
					if ((xmlSchemaComplexType = (xmlSchemaElement.SchemaType as XmlSchemaComplexType)) != null)
					{
						MBObjectManager.InjectAttribute(xmlSchemaComplexType, xsType, name);
						MBObjectManager.WalkParticle(xmlSchemaComplexType.Particle, xsType, name);
					}
					XmlSchemaComplexType xmlSchemaComplexType2;
					if ((xmlSchemaComplexType2 = (xmlSchemaElement.ElementSchemaType as XmlSchemaComplexType)) != null)
					{
						MBObjectManager.InjectAttribute(xmlSchemaComplexType2, xsType, name);
						MBObjectManager.WalkParticle(xmlSchemaComplexType2.Particle, xsType, name);
					}
					return;
				}
				XmlSchemaGroupBase xmlSchemaGroupBase;
				if ((xmlSchemaGroupBase = (particle as XmlSchemaGroupBase)) != null)
				{
					foreach (XmlSchemaObject xmlSchemaObject in xmlSchemaGroupBase.Items)
					{
						XmlSchemaElement xmlSchemaElement2;
						XmlSchemaGroupBase particle2;
						if ((xmlSchemaElement2 = (xmlSchemaObject as XmlSchemaElement)) != null)
						{
							XmlSchemaComplexType xmlSchemaComplexType3;
							if ((xmlSchemaComplexType3 = (xmlSchemaElement2.SchemaType as XmlSchemaComplexType)) != null)
							{
								MBObjectManager.InjectAttribute(xmlSchemaComplexType3, xsType, name);
								MBObjectManager.WalkParticle(xmlSchemaComplexType3.Particle, xsType, name);
							}
							XmlSchemaComplexType xmlSchemaComplexType4;
							if ((xmlSchemaComplexType4 = (xmlSchemaElement2.ElementSchemaType as XmlSchemaComplexType)) != null)
							{
								MBObjectManager.InjectAttribute(xmlSchemaComplexType4, xsType, name);
								MBObjectManager.WalkParticle(xmlSchemaComplexType4.Particle, xsType, name);
							}
						}
						else if ((particle2 = (xmlSchemaObject as XmlSchemaGroupBase)) != null)
						{
							MBObjectManager.WalkParticle(particle2, xsType, name);
						}
					}
				}
			}
		}

		// Token: 0x0600006A RID: 106 RVA: 0x0000446C File Offset: 0x0000266C
		private static void ValidationEventHandler(object sender, ValidationEventArgs e)
		{
			XmlReader xmlReader = (XmlReader)sender;
			string text = string.Empty;
			XmlSeverityType severity = e.Severity;
			if (severity != XmlSeverityType.Error)
			{
				if (severity == XmlSeverityType.Warning)
				{
					text = text + "Warning: " + e.Message;
				}
			}
			else
			{
				text = text + "Error: " + e.Message;
			}
			text = string.Concat(new string[]
			{
				text,
				"\nNode: ",
				xmlReader.Name,
				"  Value: ",
				xmlReader.Value
			});
			text = text + "\nLine: " + e.Exception.LineNumber;
			text = text + "\nXML Path: " + xmlReader.BaseURI;
			Debug.Print(text, 0, Debug.DebugColor.Red, 17592186044416UL);
		}

		// Token: 0x0600006B RID: 107 RVA: 0x00004530 File Offset: 0x00002730
		private static XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation = false)
		{
			Debug.Print("opening " + xmlPath, 0, Debug.DebugColor.White, 17592186044416UL);
			XmlDocument xmlDocument = new XmlDocument();
			StreamReader streamReader = new StreamReader(xmlPath);
			string xml = streamReader.ReadToEnd();
			if (!forceSkipValidation)
			{
				MBObjectManager.LoadXmlWithValidation(xmlPath, xsdPath, xmlDocument);
			}
			else
			{
				xmlDocument.LoadXml(xml);
			}
			streamReader.Close();
			return xmlDocument;
		}

		// Token: 0x0600006C RID: 108 RVA: 0x00004588 File Offset: 0x00002788
		public void LoadXml(XmlDocument doc, bool isDevelopment = false)
		{
			int i = 0;
			bool flag = false;
			string typeName = null;
			while (i < doc.ChildNodes.Count)
			{
				int i2 = i;
				foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
				{
					if (objectTypeRecord.ElementListName == doc.ChildNodes[i2].Name)
					{
						typeName = objectTypeRecord.ElementName;
						flag = true;
						break;
					}
				}
				if (flag)
				{
					break;
				}
				i++;
			}
			if (flag)
			{
				for (XmlNode xmlNode = doc.ChildNodes[i].ChildNodes[0]; xmlNode != null; xmlNode = xmlNode.NextSibling)
				{
					if (xmlNode.NodeType != XmlNodeType.Comment)
					{
						string value = xmlNode.Attributes["id"].Value;
						MBObjectBase presumedObject = this.GetPresumedObject(typeName, value, true);
						presumedObject.Deserialize(this, xmlNode);
						presumedObject.AfterInitialized();
					}
				}
			}
		}

		// Token: 0x0600006D RID: 109 RVA: 0x00004688 File Offset: 0x00002888
		public MBObjectBase CreateObjectFromXmlNode(XmlNode node)
		{
			return this.CreateObjectFromXmlNode(node, node.Name);
		}

		// Token: 0x0600006E RID: 110 RVA: 0x00004698 File Offset: 0x00002898
		public MBObjectBase CreateObjectFromXmlNode(XmlNode node, string typeName)
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ElementName == typeName)
				{
					string value = node.Attributes["id"].Value;
					MBObjectBase presumedObject = this.GetPresumedObject(objectTypeRecord.ElementName, value, false);
					presumedObject.Deserialize(this, node);
					presumedObject.AfterInitialized();
					return presumedObject;
				}
			}
			return null;
		}

		// Token: 0x0600006F RID: 111 RVA: 0x0000472C File Offset: 0x0000292C
		public MBObjectBase CreateObjectWithoutDeserialize(XmlNode node)
		{
			string name = node.Name;
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				if (objectTypeRecord.ElementName == name)
				{
					string value = node.Attributes["id"].Value;
					MBObjectBase presumedObject = this.GetPresumedObject(objectTypeRecord.ElementName, value, false);
					presumedObject.Initialize();
					presumedObject.AfterInitialized();
					return presumedObject;
				}
			}
			return null;
		}

		// Token: 0x06000070 RID: 112 RVA: 0x000047C8 File Offset: 0x000029C8
		public void UnregisterNonReadyObjects()
		{
			foreach (IEnumerable enumerable in this.ObjectTypeRecords)
			{
				List<MBObjectBase> list = new List<MBObjectBase>();
				foreach (object obj in enumerable)
				{
					MBObjectBase mbobjectBase = (MBObjectBase)obj;
					if (!mbobjectBase.IsReady)
					{
						list.Add(mbobjectBase);
					}
				}
				if (list.Any<MBObjectBase>())
				{
					foreach (MBObjectBase mbobjectBase2 in list)
					{
						Debug.Print("Null object reference found with ID: " + mbobjectBase2.StringId, 0, Debug.DebugColor.White, 17592186044416UL);
						this.UnregisterObject(mbobjectBase2);
					}
				}
			}
		}

		// Token: 0x06000071 RID: 113 RVA: 0x000048D8 File Offset: 0x00002AD8
		public void ClearAllObjects()
		{
			for (int i = this.ObjectTypeRecords.Count - 1; i >= 0; i--)
			{
				List<MBObjectBase> list = new List<MBObjectBase>();
				foreach (object obj in this.ObjectTypeRecords[i])
				{
					MBObjectBase item = (MBObjectBase)obj;
					list.Add(item);
				}
				foreach (MBObjectBase obj2 in list)
				{
					this.ObjectTypeRecords[i].UnregisterMBObject(obj2);
					this.AfterUnregisterObject(obj2);
				}
			}
		}

		// Token: 0x06000072 RID: 114 RVA: 0x000049B4 File Offset: 0x00002BB4
		public void ClearAllObjectsWithType(Type type)
		{
			for (int i = this.ObjectTypeRecords.Count - 1; i >= 0; i--)
			{
				if (this.ObjectTypeRecords[i].ObjectClass == type)
				{
					List<MBObjectBase> list = new List<MBObjectBase>();
					foreach (object obj in this.ObjectTypeRecords[i])
					{
						MBObjectBase item = (MBObjectBase)obj;
						list.Add(item);
					}
					foreach (MBObjectBase obj2 in list)
					{
						this.UnregisterObject(obj2);
					}
				}
			}
		}

		// Token: 0x06000073 RID: 115 RVA: 0x00004A94 File Offset: 0x00002C94
		public T ReadObjectReferenceFromXml<T>(string attributeName, XmlNode node) where T : MBObjectBase
		{
			if (node.Attributes[attributeName] == null)
			{
				return default(T);
			}
			string value = node.Attributes[attributeName].Value;
			string text = value.Split(".".ToCharArray())[0];
			if (text == value)
			{
				throw new MBInvalidReferenceException(value);
			}
			string text2 = value.Split(".".ToCharArray())[1];
			if (text == string.Empty || text2 == string.Empty)
			{
				throw new MBInvalidReferenceException(value);
			}
			return this.GetPresumedObject(text, text2, false) as T;
		}

		// Token: 0x06000074 RID: 116 RVA: 0x00004B38 File Offset: 0x00002D38
		public MBObjectBase ReadObjectReferenceFromXml(string attributeName, Type objectType, XmlNode node)
		{
			if (node.Attributes[attributeName] == null)
			{
				return null;
			}
			string value = node.Attributes[attributeName].Value;
			string text = value.Split(".".ToCharArray())[0];
			if (text == value)
			{
				throw new MBInvalidReferenceException(value);
			}
			string text2 = value.Split(".".ToCharArray())[1];
			if (text == string.Empty || text2 == string.Empty)
			{
				throw new MBInvalidReferenceException(value);
			}
			return this.GetPresumedObject(text, text2, false);
		}

		// Token: 0x06000075 RID: 117 RVA: 0x00004BC8 File Offset: 0x00002DC8
		public T CreateObject<T>(string stringId) where T : MBObjectBase, new()
		{
			T t = Activator.CreateInstance<T>();
			t.StringId = stringId;
			this.RegisterObject<T>(t);
			if (this._handlers != null)
			{
				foreach (IObjectManagerHandler objectManagerHandler in this._handlers)
				{
					objectManagerHandler.AfterCreateObject(t);
				}
			}
			return t;
		}

		// Token: 0x06000076 RID: 118 RVA: 0x00004C44 File Offset: 0x00002E44
		public T CreateObject<T>() where T : MBObjectBase, new()
		{
			return this.CreateObject<T>(typeof(T).Name.ToString() + "_1");
		}

		// Token: 0x06000077 RID: 119 RVA: 0x00004C6C File Offset: 0x00002E6C
		public void DebugPrint(PrintOutputDelegate printOutput)
		{
			printOutput("-Printing MBObjectManager Debug-");
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				printOutput(objectTypeRecord.DebugBasicDump());
			}
		}

		// Token: 0x06000078 RID: 120 RVA: 0x00004CD0 File Offset: 0x00002ED0
		public void AddHandler(IObjectManagerHandler handler)
		{
			if (this._handlers == null)
			{
				this._handlers = new List<IObjectManagerHandler>();
			}
			this._handlers.Add(handler);
		}

		// Token: 0x06000079 RID: 121 RVA: 0x00004CF1 File Offset: 0x00002EF1
		public void RemoveHandler(IObjectManagerHandler handler)
		{
			this._handlers.Remove(handler);
		}

		// Token: 0x0600007A RID: 122 RVA: 0x00004D00 File Offset: 0x00002F00
		public string DebugDump()
		{
			string text = "";
			text += "--------------------------------------\r\n";
			text += "----Printing MBObjectManager Debug----\r\n";
			text += "--------------------------------------\r\n";
			text += "\r\n";
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				text += objectTypeRecord.DebugDump();
			}
			File.WriteAllText("mbobjectmanagerdump.txt", text);
			return text;
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00004D9C File Offset: 0x00002F9C
		public void ReInitialize()
		{
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords.ToList<MBObjectManager.IObjectTypeRecord>())
			{
				objectTypeRecord.ReInitialize();
			}
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00004DF4 File Offset: 0x00002FF4
		public string GetObjectTypeIds()
		{
			string text = "";
			foreach (MBObjectManager.IObjectTypeRecord objectTypeRecord in this.ObjectTypeRecords)
			{
				text = string.Concat(new object[]
				{
					text,
					objectTypeRecord.TypeNo,
					" - ",
					objectTypeRecord.GetType().FullName,
					"\n"
				});
			}
			return text;
		}

		// Token: 0x0400000A RID: 10
		internal List<MBObjectManager.IObjectTypeRecord> ObjectTypeRecords = new List<MBObjectManager.IObjectTypeRecord>();

		// Token: 0x0400000B RID: 11
		private List<IObjectManagerHandler> _handlers;

		// Token: 0x02000014 RID: 20
		internal interface IObjectTypeRecord : IEnumerable
		{
			// Token: 0x1700000B RID: 11
			// (get) Token: 0x060000A0 RID: 160
			bool AutoCreate { get; }

			// Token: 0x1700000C RID: 12
			// (get) Token: 0x060000A1 RID: 161
			string ElementName { get; }

			// Token: 0x1700000D RID: 13
			// (get) Token: 0x060000A2 RID: 162
			string ElementListName { get; }

			// Token: 0x1700000E RID: 14
			// (get) Token: 0x060000A3 RID: 163
			Type ObjectClass { get; }

			// Token: 0x1700000F RID: 15
			// (get) Token: 0x060000A4 RID: 164
			uint TypeNo { get; }

			// Token: 0x17000010 RID: 16
			// (get) Token: 0x060000A5 RID: 165
			bool IsTemporary { get; }

			// Token: 0x060000A6 RID: 166
			void ReInitialize();

			// Token: 0x060000A7 RID: 167
			MBObjectBase CreatePresumedMBObject(string objectName);

			// Token: 0x060000A8 RID: 168
			void RegisterMBObject(MBObjectBase obj, bool presumed, out MBObjectBase registeredObject);

			// Token: 0x060000A9 RID: 169
			void RegisterMBObjectWithoutInitialization(MBObjectBase obj);

			// Token: 0x060000AA RID: 170
			void UnregisterMBObject(MBObjectBase obj);

			// Token: 0x060000AB RID: 171
			MBObjectBase GetFirstMBObject();

			// Token: 0x060000AC RID: 172
			MBObjectBase GetMBObject(string objId);

			// Token: 0x060000AD RID: 173
			MBObjectBase GetMBObject(MBGUID objId);

			// Token: 0x060000AE RID: 174
			bool ContainsObject(string objId);

			// Token: 0x060000AF RID: 175
			string DebugDump();

			// Token: 0x060000B0 RID: 176
			string DebugBasicDump();

			// Token: 0x060000B1 RID: 177
			IEnumerable GetList();

			// Token: 0x060000B2 RID: 178
			void PreAfterLoad();

			// Token: 0x060000B3 RID: 179
			void AfterLoad();
		}

		// Token: 0x02000015 RID: 21
		internal class ObjectTypeRecord<T> : MBObjectManager.IObjectTypeRecord, IEnumerable, IEnumerable<T> where T : MBObjectBase
		{
			// Token: 0x17000011 RID: 17
			// (get) Token: 0x060000B4 RID: 180 RVA: 0x0000568B File Offset: 0x0000388B
			bool MBObjectManager.IObjectTypeRecord.AutoCreate
			{
				get
				{
					return this._autoCreate;
				}
			}

			// Token: 0x17000012 RID: 18
			// (get) Token: 0x060000B5 RID: 181 RVA: 0x00005693 File Offset: 0x00003893
			string MBObjectManager.IObjectTypeRecord.ElementName
			{
				get
				{
					return this._elementName;
				}
			}

			// Token: 0x17000013 RID: 19
			// (get) Token: 0x060000B6 RID: 182 RVA: 0x0000569B File Offset: 0x0000389B
			string MBObjectManager.IObjectTypeRecord.ElementListName
			{
				get
				{
					return this._elementListName;
				}
			}

			// Token: 0x17000014 RID: 20
			// (get) Token: 0x060000B7 RID: 183 RVA: 0x000056A3 File Offset: 0x000038A3
			Type MBObjectManager.IObjectTypeRecord.ObjectClass
			{
				get
				{
					return typeof(T);
				}
			}

			// Token: 0x17000015 RID: 21
			// (get) Token: 0x060000B8 RID: 184 RVA: 0x000056AF File Offset: 0x000038AF
			uint MBObjectManager.IObjectTypeRecord.TypeNo
			{
				get
				{
					return this._typeNo;
				}
			}

			// Token: 0x17000016 RID: 22
			// (get) Token: 0x060000B9 RID: 185 RVA: 0x000056B7 File Offset: 0x000038B7
			bool MBObjectManager.IObjectTypeRecord.IsTemporary
			{
				get
				{
					return this._isTemporary;
				}
			}

			// Token: 0x17000017 RID: 23
			// (get) Token: 0x060000BA RID: 186 RVA: 0x000056BF File Offset: 0x000038BF
			internal MBList<T> RegisteredObjectsList { get; }

			// Token: 0x060000BB RID: 187 RVA: 0x000056C8 File Offset: 0x000038C8
			internal ObjectTypeRecord(uint newTypeNo, string classPrefix, string classListPrefix, bool autoCreate, bool isTemporary)
			{
				this._typeNo = newTypeNo;
				this._elementName = classPrefix;
				this._elementListName = classListPrefix;
				this._autoCreate = autoCreate;
				this._isTemporary = isTemporary;
				this._registeredObjects = new Dictionary<string, T>();
				this._registeredObjectsWithGuid = new Dictionary<MBGUID, T>();
				this.RegisteredObjectsList = new MBList<T>();
				this._objCount = 0U;
			}

			// Token: 0x060000BC RID: 188 RVA: 0x00005728 File Offset: 0x00003928
			void MBObjectManager.IObjectTypeRecord.ReInitialize()
			{
				uint num = 0U;
				foreach (T t in this.RegisteredObjectsList)
				{
					uint subId = t.Id.SubId;
					if (subId > num)
					{
						num = subId;
					}
				}
				this._objCount = num + 1U;
			}

			// Token: 0x060000BD RID: 189 RVA: 0x00005798 File Offset: 0x00003998
			IEnumerator<T> IEnumerable<!0>.GetEnumerator()
			{
				return this.EnumerateElements();
			}

			// Token: 0x060000BE RID: 190 RVA: 0x000057A0 File Offset: 0x000039A0
			IEnumerator IEnumerable.GetEnumerator()
			{
				return this.EnumerateElements();
			}

			// Token: 0x060000BF RID: 191 RVA: 0x000057A8 File Offset: 0x000039A8
			internal MBGUID GetNewId()
			{
				uint typeNo = this._typeNo;
				uint num = this._objCount + 1U;
				this._objCount = num;
				return new MBGUID(typeNo, num);
			}

			// Token: 0x060000C0 RID: 192 RVA: 0x000057D1 File Offset: 0x000039D1
			MBObjectBase MBObjectManager.IObjectTypeRecord.CreatePresumedMBObject(string objectName)
			{
				return this.CreatePresumedObject(objectName);
			}

			// Token: 0x060000C1 RID: 193 RVA: 0x000057E0 File Offset: 0x000039E0
			private T CreatePresumedObject(string stringId)
			{
				T t = default(T);
				ConstructorInfo constructor = typeof(T).GetConstructor(new Type[]
				{
					typeof(string)
				});
				if (constructor != null)
				{
					t = (T)((object)constructor.Invoke(new object[]
					{
						stringId
					}));
				}
				else
				{
					t = Activator.CreateInstance<T>();
					t.StringId = stringId;
				}
				t.IsReady = false;
				t.IsInitialized = false;
				return t;
			}

			// Token: 0x060000C2 RID: 194 RVA: 0x00005864 File Offset: 0x00003A64
			MBObjectBase MBObjectManager.IObjectTypeRecord.GetMBObject(string objId)
			{
				return this.GetObject(objId);
			}

			// Token: 0x060000C3 RID: 195 RVA: 0x00005872 File Offset: 0x00003A72
			MBObjectBase MBObjectManager.IObjectTypeRecord.GetFirstMBObject()
			{
				return this.GetFirstObject();
			}

			// Token: 0x060000C4 RID: 196 RVA: 0x00005880 File Offset: 0x00003A80
			internal T GetFirstObject()
			{
				if (this.RegisteredObjectsList.Count <= 0)
				{
					return default(T);
				}
				return this.RegisteredObjectsList[0];
			}

			// Token: 0x060000C5 RID: 197 RVA: 0x000058B4 File Offset: 0x00003AB4
			internal T GetObject(string objId)
			{
				T result;
				this._registeredObjects.TryGetValue(objId, out result);
				return result;
			}

			// Token: 0x060000C6 RID: 198 RVA: 0x000058D1 File Offset: 0x00003AD1
			bool MBObjectManager.IObjectTypeRecord.ContainsObject(string objId)
			{
				return this._registeredObjects.ContainsKey(objId);
			}

			// Token: 0x060000C7 RID: 199 RVA: 0x000058E0 File Offset: 0x00003AE0
			public MBObjectBase GetMBObject(MBGUID objId)
			{
				T t = default(T);
				this._registeredObjectsWithGuid.TryGetValue(objId, out t);
				return t;
			}

			// Token: 0x060000C8 RID: 200 RVA: 0x0000590C File Offset: 0x00003B0C
			void MBObjectManager.IObjectTypeRecord.RegisterMBObjectWithoutInitialization(MBObjectBase mbObject)
			{
				T t = (T)((object)mbObject);
				if (!string.IsNullOrEmpty(t.StringId) && t.Id.InternalValue != 0U && !this._registeredObjects.ContainsKey(t.StringId))
				{
					this._registeredObjects.Add(t.StringId, t);
					this._registeredObjectsWithGuid.Add(t.Id, t);
					this.RegisteredObjectsList.Add(t);
				}
			}

			// Token: 0x060000C9 RID: 201 RVA: 0x00005999 File Offset: 0x00003B99
			void MBObjectManager.IObjectTypeRecord.RegisterMBObject(MBObjectBase obj, bool presumed, out MBObjectBase registeredObject)
			{
				if (obj is T)
				{
					this.RegisterObject(obj as T, presumed, out registeredObject);
					return;
				}
				registeredObject = null;
			}

			// Token: 0x060000CA RID: 202 RVA: 0x000059BC File Offset: 0x00003BBC
			internal void RegisterObject(T obj, bool presumed, out MBObjectBase registeredObject)
			{
				T t;
				if (this._registeredObjects.TryGetValue(obj.StringId, out t))
				{
					if (t == obj || presumed)
					{
						registeredObject = t;
						return;
					}
					ValueTuple<string, long> idParts = this.GetIdParts(obj.StringId);
					string item = idParts.Item1;
					long num = idParts.Item2;
					if (this._registeredObjects.ContainsKey(obj.StringId))
					{
						num = (long)((ulong)this._objCount);
						obj.StringId = item + num.ToString();
						while (this._registeredObjects.ContainsKey(obj.StringId))
						{
							num += 1L;
							obj.StringId = item + num.ToString();
						}
					}
				}
				this._registeredObjects.Add(obj.StringId, obj);
				obj.Id = this.GetNewId();
				this._registeredObjectsWithGuid.Add(obj.Id, obj);
				this.RegisteredObjectsList.Add(obj);
				obj.IsReady = !presumed;
				obj.OnRegistered();
				registeredObject = obj;
			}

			// Token: 0x060000CB RID: 203 RVA: 0x00005AFC File Offset: 0x00003CFC
			[return: TupleElementNames(new string[]
			{
				"str",
				"number"
			})]
			private ValueTuple<string, long> GetIdParts(string stringId)
			{
				int num = stringId.Length - 1;
				while (num > 0 && char.IsDigit(stringId[num]))
				{
					num--;
				}
				string item = stringId.Substring(0, num + 1);
				long item2 = 0L;
				if (num < stringId.Length - 1)
				{
					long.TryParse(stringId.Substring(num + 1, stringId.Length - num - 1), out item2);
				}
				return new ValueTuple<string, long>(item, item2);
			}

			// Token: 0x060000CC RID: 204 RVA: 0x00005B63 File Offset: 0x00003D63
			void MBObjectManager.IObjectTypeRecord.UnregisterMBObject(MBObjectBase obj)
			{
				if (obj is T)
				{
					this.UnregisterObject((T)((object)obj));
					return;
				}
				throw new MBIllegalRegisterException();
			}

			// Token: 0x060000CD RID: 205 RVA: 0x00005B80 File Offset: 0x00003D80
			private void UnregisterObject(T obj)
			{
				obj.OnUnregistered();
				if (this._registeredObjects.ContainsKey(obj.StringId) && this._registeredObjects[obj.StringId] == obj)
				{
					this._registeredObjects.Remove(obj.StringId);
				}
				if (this._registeredObjectsWithGuid.ContainsKey(obj.Id) && this._registeredObjectsWithGuid[obj.Id] == obj)
				{
					this._registeredObjectsWithGuid.Remove(obj.Id);
				}
				this.RegisteredObjectsList.Remove(obj);
			}

			// Token: 0x060000CE RID: 206 RVA: 0x00005C49 File Offset: 0x00003E49
			internal MBReadOnlyList<T> GetObjectsList()
			{
				return this.RegisteredObjectsList;
			}

			// Token: 0x060000CF RID: 207 RVA: 0x00005C51 File Offset: 0x00003E51
			IEnumerable MBObjectManager.IObjectTypeRecord.GetList()
			{
				return this.RegisteredObjectsList;
			}

			// Token: 0x060000D0 RID: 208 RVA: 0x00005C5C File Offset: 0x00003E5C
			string MBObjectManager.IObjectTypeRecord.DebugDump()
			{
				string text = "";
				text += "**************************************\r\n";
				text = string.Concat(new object[]
				{
					text,
					this._elementName,
					" ",
					this._objCount,
					"\r\n"
				});
				text += "**************************************\r\n";
				text += "\r\n";
				foreach (KeyValuePair<MBGUID, T> keyValuePair in this._registeredObjectsWithGuid)
				{
					text = string.Concat(new string[]
					{
						text,
						keyValuePair.Key.ToString(),
						" ",
						keyValuePair.Value.ToString(),
						"\r\n"
					});
				}
				return text;
			}

			// Token: 0x060000D1 RID: 209 RVA: 0x00005D58 File Offset: 0x00003F58
			string MBObjectManager.IObjectTypeRecord.DebugBasicDump()
			{
				return this._elementName + " " + this._objCount;
			}

			// Token: 0x060000D2 RID: 210 RVA: 0x00005D75 File Offset: 0x00003F75
			private IEnumerator<T> EnumerateElements()
			{
				int num;
				for (int i = 0; i < this.RegisteredObjectsList.Count; i = num + 1)
				{
					yield return this.RegisteredObjectsList[i];
					num = i;
				}
				yield break;
			}

			// Token: 0x060000D3 RID: 211 RVA: 0x00005D84 File Offset: 0x00003F84
			void MBObjectManager.IObjectTypeRecord.PreAfterLoad()
			{
				for (int i = this.RegisteredObjectsList.Count - 1; i >= 0; i--)
				{
					this.RegisteredObjectsList[i].PreAfterLoadInternal();
				}
			}

			// Token: 0x060000D4 RID: 212 RVA: 0x00005DC0 File Offset: 0x00003FC0
			void MBObjectManager.IObjectTypeRecord.AfterLoad()
			{
				for (int i = this.RegisteredObjectsList.Count - 1; i >= 0; i--)
				{
					this.RegisteredObjectsList[i].AfterLoadInternal();
				}
			}

			// Token: 0x04000014 RID: 20
			private readonly bool _autoCreate;

			// Token: 0x04000015 RID: 21
			private readonly string _elementName;

			// Token: 0x04000016 RID: 22
			private readonly string _elementListName;

			// Token: 0x04000017 RID: 23
			private uint _objCount;

			// Token: 0x04000018 RID: 24
			private readonly uint _typeNo;

			// Token: 0x04000019 RID: 25
			private readonly bool _isTemporary;

			// Token: 0x0400001A RID: 26
			private readonly Dictionary<string, T> _registeredObjects;

			// Token: 0x0400001B RID: 27
			private readonly Dictionary<MBGUID, T> _registeredObjectsWithGuid;
		}
	}
}
