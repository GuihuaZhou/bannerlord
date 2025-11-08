using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TaleWorlds.Library
{
	// Token: 0x020000A3 RID: 163
	public abstract class ViewModel : IViewModel, INotifyPropertyChanged
	{
		// Token: 0x14000016 RID: 22
		// (add) Token: 0x0600062C RID: 1580 RVA: 0x000157A2 File Offset: 0x000139A2
		// (remove) Token: 0x0600062D RID: 1581 RVA: 0x000157C3 File Offset: 0x000139C3
		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
				if (this._eventHandlers == null)
				{
					this._eventHandlers = new List<PropertyChangedEventHandler>();
				}
				this._eventHandlers.Add(value);
			}
			remove
			{
				if (this._eventHandlers != null)
				{
					this._eventHandlers.Remove(value);
				}
			}
		}

		// Token: 0x14000017 RID: 23
		// (add) Token: 0x0600062E RID: 1582 RVA: 0x000157DA File Offset: 0x000139DA
		// (remove) Token: 0x0600062F RID: 1583 RVA: 0x000157FB File Offset: 0x000139FB
		public event PropertyChangedWithValueEventHandler PropertyChangedWithValue
		{
			add
			{
				if (this._eventHandlersWithValue == null)
				{
					this._eventHandlersWithValue = new List<PropertyChangedWithValueEventHandler>();
				}
				this._eventHandlersWithValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithValue != null)
				{
					this._eventHandlersWithValue.Remove(value);
				}
			}
		}

		// Token: 0x14000018 RID: 24
		// (add) Token: 0x06000630 RID: 1584 RVA: 0x00015812 File Offset: 0x00013A12
		// (remove) Token: 0x06000631 RID: 1585 RVA: 0x00015833 File Offset: 0x00013A33
		public event PropertyChangedWithBoolValueEventHandler PropertyChangedWithBoolValue
		{
			add
			{
				if (this._eventHandlersWithBoolValue == null)
				{
					this._eventHandlersWithBoolValue = new List<PropertyChangedWithBoolValueEventHandler>();
				}
				this._eventHandlersWithBoolValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithBoolValue != null)
				{
					this._eventHandlersWithBoolValue.Remove(value);
				}
			}
		}

		// Token: 0x14000019 RID: 25
		// (add) Token: 0x06000632 RID: 1586 RVA: 0x0001584A File Offset: 0x00013A4A
		// (remove) Token: 0x06000633 RID: 1587 RVA: 0x0001586B File Offset: 0x00013A6B
		public event PropertyChangedWithIntValueEventHandler PropertyChangedWithIntValue
		{
			add
			{
				if (this._eventHandlersWithIntValue == null)
				{
					this._eventHandlersWithIntValue = new List<PropertyChangedWithIntValueEventHandler>();
				}
				this._eventHandlersWithIntValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithIntValue != null)
				{
					this._eventHandlersWithIntValue.Remove(value);
				}
			}
		}

		// Token: 0x1400001A RID: 26
		// (add) Token: 0x06000634 RID: 1588 RVA: 0x00015882 File Offset: 0x00013A82
		// (remove) Token: 0x06000635 RID: 1589 RVA: 0x000158A3 File Offset: 0x00013AA3
		public event PropertyChangedWithFloatValueEventHandler PropertyChangedWithFloatValue
		{
			add
			{
				if (this._eventHandlersWithFloatValue == null)
				{
					this._eventHandlersWithFloatValue = new List<PropertyChangedWithFloatValueEventHandler>();
				}
				this._eventHandlersWithFloatValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithFloatValue != null)
				{
					this._eventHandlersWithFloatValue.Remove(value);
				}
			}
		}

		// Token: 0x1400001B RID: 27
		// (add) Token: 0x06000636 RID: 1590 RVA: 0x000158BA File Offset: 0x00013ABA
		// (remove) Token: 0x06000637 RID: 1591 RVA: 0x000158DB File Offset: 0x00013ADB
		public event PropertyChangedWithUIntValueEventHandler PropertyChangedWithUIntValue
		{
			add
			{
				if (this._eventHandlersWithUIntValue == null)
				{
					this._eventHandlersWithUIntValue = new List<PropertyChangedWithUIntValueEventHandler>();
				}
				this._eventHandlersWithUIntValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithUIntValue != null)
				{
					this._eventHandlersWithUIntValue.Remove(value);
				}
			}
		}

		// Token: 0x1400001C RID: 28
		// (add) Token: 0x06000638 RID: 1592 RVA: 0x000158F2 File Offset: 0x00013AF2
		// (remove) Token: 0x06000639 RID: 1593 RVA: 0x00015913 File Offset: 0x00013B13
		public event PropertyChangedWithColorValueEventHandler PropertyChangedWithColorValue
		{
			add
			{
				if (this._eventHandlersWithColorValue == null)
				{
					this._eventHandlersWithColorValue = new List<PropertyChangedWithColorValueEventHandler>();
				}
				this._eventHandlersWithColorValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithColorValue != null)
				{
					this._eventHandlersWithColorValue.Remove(value);
				}
			}
		}

		// Token: 0x1400001D RID: 29
		// (add) Token: 0x0600063A RID: 1594 RVA: 0x0001592A File Offset: 0x00013B2A
		// (remove) Token: 0x0600063B RID: 1595 RVA: 0x0001594B File Offset: 0x00013B4B
		public event PropertyChangedWithDoubleValueEventHandler PropertyChangedWithDoubleValue
		{
			add
			{
				if (this._eventHandlersWithDoubleValue == null)
				{
					this._eventHandlersWithDoubleValue = new List<PropertyChangedWithDoubleValueEventHandler>();
				}
				this._eventHandlersWithDoubleValue.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithDoubleValue != null)
				{
					this._eventHandlersWithDoubleValue.Remove(value);
				}
			}
		}

		// Token: 0x1400001E RID: 30
		// (add) Token: 0x0600063C RID: 1596 RVA: 0x00015962 File Offset: 0x00013B62
		// (remove) Token: 0x0600063D RID: 1597 RVA: 0x00015983 File Offset: 0x00013B83
		public event PropertyChangedWithVec2ValueEventHandler PropertyChangedWithVec2Value
		{
			add
			{
				if (this._eventHandlersWithVec2Value == null)
				{
					this._eventHandlersWithVec2Value = new List<PropertyChangedWithVec2ValueEventHandler>();
				}
				this._eventHandlersWithVec2Value.Add(value);
			}
			remove
			{
				if (this._eventHandlersWithVec2Value != null)
				{
					this._eventHandlersWithVec2Value.Remove(value);
				}
			}
		}

		// Token: 0x0600063E RID: 1598 RVA: 0x0001599C File Offset: 0x00013B9C
		protected ViewModel()
		{
			this._type = base.GetType();
			ViewModel.DataSourceTypeBindingPropertiesCollection dataSourceTypeBindingPropertiesCollection;
			ViewModel._cachedViewModelProperties.TryGetValue(this._type, out dataSourceTypeBindingPropertiesCollection);
			if (dataSourceTypeBindingPropertiesCollection == null)
			{
				this._propertiesAndMethods = ViewModel.GetPropertiesOfType(this._type);
				ViewModel._cachedViewModelProperties.Add(this._type, this._propertiesAndMethods);
				return;
			}
			this._propertiesAndMethods = dataSourceTypeBindingPropertiesCollection;
		}

		// Token: 0x0600063F RID: 1599 RVA: 0x00015A00 File Offset: 0x00013C00
		private PropertyInfo GetProperty(string name)
		{
			PropertyInfo result;
			if (this._propertiesAndMethods != null && this._propertiesAndMethods.Properties.TryGetValue(name, out result))
			{
				return result;
			}
			return null;
		}

		// Token: 0x06000640 RID: 1600 RVA: 0x00015A2D File Offset: 0x00013C2D
		protected bool SetField<T>(ref T field, T value, string propertyName)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return false;
			}
			field = value;
			this.OnPropertyChanged(propertyName);
			return true;
		}

		// Token: 0x06000641 RID: 1601 RVA: 0x00015A54 File Offset: 0x00013C54
		public void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlers != null)
			{
				for (int i = 0; i < this._eventHandlers.Count; i++)
				{
					PropertyChangedEventHandler propertyChangedEventHandler = this._eventHandlers[i];
					PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
					propertyChangedEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000642 RID: 1602 RVA: 0x00015A9C File Offset: 0x00013C9C
		public void OnPropertyChangedWithValue<T>(T value, [CallerMemberName] string propertyName = null) where T : class
		{
			if (this._eventHandlersWithValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithValue.Count; i++)
				{
					PropertyChangedWithValueEventHandler propertyChangedWithValueEventHandler = this._eventHandlersWithValue[i];
					PropertyChangedWithValueEventArgs e = new PropertyChangedWithValueEventArgs(propertyName, value);
					propertyChangedWithValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000643 RID: 1603 RVA: 0x00015AE8 File Offset: 0x00013CE8
		public void OnPropertyChangedWithValue(bool value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithBoolValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithBoolValue.Count; i++)
				{
					PropertyChangedWithBoolValueEventHandler propertyChangedWithBoolValueEventHandler = this._eventHandlersWithBoolValue[i];
					PropertyChangedWithBoolValueEventArgs e = new PropertyChangedWithBoolValueEventArgs(propertyName, value);
					propertyChangedWithBoolValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000644 RID: 1604 RVA: 0x00015B30 File Offset: 0x00013D30
		public void OnPropertyChangedWithValue(int value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithIntValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithIntValue.Count; i++)
				{
					PropertyChangedWithIntValueEventHandler propertyChangedWithIntValueEventHandler = this._eventHandlersWithIntValue[i];
					PropertyChangedWithIntValueEventArgs e = new PropertyChangedWithIntValueEventArgs(propertyName, value);
					propertyChangedWithIntValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000645 RID: 1605 RVA: 0x00015B78 File Offset: 0x00013D78
		public void OnPropertyChangedWithValue(float value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithFloatValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithFloatValue.Count; i++)
				{
					PropertyChangedWithFloatValueEventHandler propertyChangedWithFloatValueEventHandler = this._eventHandlersWithFloatValue[i];
					PropertyChangedWithFloatValueEventArgs e = new PropertyChangedWithFloatValueEventArgs(propertyName, value);
					propertyChangedWithFloatValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000646 RID: 1606 RVA: 0x00015BC0 File Offset: 0x00013DC0
		public void OnPropertyChangedWithValue(uint value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithUIntValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithUIntValue.Count; i++)
				{
					PropertyChangedWithUIntValueEventHandler propertyChangedWithUIntValueEventHandler = this._eventHandlersWithUIntValue[i];
					PropertyChangedWithUIntValueEventArgs e = new PropertyChangedWithUIntValueEventArgs(propertyName, value);
					propertyChangedWithUIntValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000647 RID: 1607 RVA: 0x00015C08 File Offset: 0x00013E08
		public void OnPropertyChangedWithValue(Color value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithColorValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithColorValue.Count; i++)
				{
					PropertyChangedWithColorValueEventHandler propertyChangedWithColorValueEventHandler = this._eventHandlersWithColorValue[i];
					PropertyChangedWithColorValueEventArgs e = new PropertyChangedWithColorValueEventArgs(propertyName, value);
					propertyChangedWithColorValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000648 RID: 1608 RVA: 0x00015C50 File Offset: 0x00013E50
		public void OnPropertyChangedWithValue(double value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithDoubleValue != null)
			{
				for (int i = 0; i < this._eventHandlersWithDoubleValue.Count; i++)
				{
					PropertyChangedWithDoubleValueEventHandler propertyChangedWithDoubleValueEventHandler = this._eventHandlersWithDoubleValue[i];
					PropertyChangedWithDoubleValueEventArgs e = new PropertyChangedWithDoubleValueEventArgs(propertyName, value);
					propertyChangedWithDoubleValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x06000649 RID: 1609 RVA: 0x00015C98 File Offset: 0x00013E98
		public void OnPropertyChangedWithValue(Vec2 value, [CallerMemberName] string propertyName = null)
		{
			if (this._eventHandlersWithVec2Value != null)
			{
				for (int i = 0; i < this._eventHandlersWithVec2Value.Count; i++)
				{
					PropertyChangedWithVec2ValueEventHandler propertyChangedWithVec2ValueEventHandler = this._eventHandlersWithVec2Value[i];
					PropertyChangedWithVec2ValueEventArgs e = new PropertyChangedWithVec2ValueEventArgs(propertyName, value);
					propertyChangedWithVec2ValueEventHandler(this, e);
				}
			}
		}

		// Token: 0x0600064A RID: 1610 RVA: 0x00015CDF File Offset: 0x00013EDF
		public object GetViewModelAtPath(BindingPath path, bool isList)
		{
			return this.GetViewModelAtPath(path);
		}

		// Token: 0x0600064B RID: 1611 RVA: 0x00015CE8 File Offset: 0x00013EE8
		public object GetViewModelAtPath(BindingPath path)
		{
			BindingPath subPath = path.SubPath;
			if (subPath != null)
			{
				PropertyInfo property = this.GetProperty(subPath.FirstNode);
				if (property != null)
				{
					object obj = property.GetGetMethod().InvokeWithLog(this, null);
					ViewModel viewModel;
					if ((viewModel = (obj as ViewModel)) != null)
					{
						return viewModel.GetViewModelAtPath(subPath);
					}
					if (obj is IMBBindingList)
					{
						return ViewModel.GetChildAtPath(obj as IMBBindingList, subPath);
					}
				}
				return null;
			}
			return this;
		}

		// Token: 0x0600064C RID: 1612 RVA: 0x00015D54 File Offset: 0x00013F54
		private static object GetChildAtPath(IMBBindingList bindingList, BindingPath path)
		{
			BindingPath subPath = path.SubPath;
			if (subPath == null)
			{
				return bindingList;
			}
			if (bindingList.Count > 0)
			{
				int num = Convert.ToInt32(subPath.FirstNode);
				if (num >= 0 && num < bindingList.Count)
				{
					object obj = bindingList[num];
					if (obj is ViewModel)
					{
						return (obj as ViewModel).GetViewModelAtPath(subPath);
					}
					if (obj is IMBBindingList)
					{
						return ViewModel.GetChildAtPath(obj as IMBBindingList, subPath);
					}
				}
			}
			return null;
		}

		// Token: 0x0600064D RID: 1613 RVA: 0x00015DC8 File Offset: 0x00013FC8
		public object GetPropertyValue(string name, PropertyTypeFeeder propertyTypeFeeder)
		{
			return this.GetPropertyValue(name);
		}

		// Token: 0x0600064E RID: 1614 RVA: 0x00015DD4 File Offset: 0x00013FD4
		public object GetPropertyValue(string name)
		{
			PropertyInfo property = this.GetProperty(name);
			object result = null;
			if (property != null)
			{
				result = property.GetGetMethod().InvokeWithLog(this, null);
			}
			return result;
		}

		// Token: 0x0600064F RID: 1615 RVA: 0x00015E04 File Offset: 0x00014004
		public Type GetPropertyType(string name)
		{
			PropertyInfo property = this.GetProperty(name);
			if (property != null)
			{
				return property.PropertyType;
			}
			return null;
		}

		// Token: 0x06000650 RID: 1616 RVA: 0x00015E2C File Offset: 0x0001402C
		public void SetPropertyValue(string name, object value)
		{
			PropertyInfo property = this.GetProperty(name);
			if (property != null)
			{
				MethodInfo setMethod = property.GetSetMethod();
				if (setMethod == null)
				{
					return;
				}
				setMethod.InvokeWithLog(this, new object[]
				{
					value
				});
			}
		}

		// Token: 0x06000651 RID: 1617 RVA: 0x00015E66 File Offset: 0x00014066
		public virtual void OnFinalize()
		{
		}

		// Token: 0x06000652 RID: 1618 RVA: 0x00015E68 File Offset: 0x00014068
		public void ExecuteCommand(string commandName, object[] parameters)
		{
			MethodInfo methodInfo = null;
			MethodInfo methodInfo2;
			if (this._propertiesAndMethods != null && this._propertiesAndMethods.Methods.TryGetValue(commandName, out methodInfo2))
			{
				methodInfo = methodInfo2;
			}
			else
			{
				Type type = this._type;
				while (type != null && methodInfo == null)
				{
					methodInfo = type.GetMethod(commandName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = type.BaseType;
				}
			}
			if (methodInfo != null)
			{
				ParameterInfo[] parameters2 = methodInfo.GetParameters();
				if (parameters2.Length == parameters.Length)
				{
					object[] array = new object[parameters.Length];
					for (int i = 0; i < parameters.Length; i++)
					{
						object obj = parameters[i];
						Type parameterType = parameters2[i].ParameterType;
						array[i] = obj;
						if (obj is string && parameterType != typeof(string))
						{
							object obj2 = ViewModel.ConvertValueTo((string)obj, parameterType);
							array[i] = obj2;
						}
					}
					if (!this.AreParametersCompatibleWithMethod(array, parameters2))
					{
						return;
					}
					methodInfo.InvokeWithLog(this, array);
					return;
				}
				else if (parameters2.Length == 0)
				{
					methodInfo.InvokeWithLog(this, null);
				}
			}
		}

		// Token: 0x06000653 RID: 1619 RVA: 0x00015F6C File Offset: 0x0001416C
		private bool AreParametersCompatibleWithMethod(object[] parameters, ParameterInfo[] methodParameters)
		{
			if (parameters.Length != methodParameters.Length)
			{
				return false;
			}
			for (int i = 0; i < parameters.Length; i++)
			{
				object obj = parameters[i];
				ParameterInfo parameterInfo = methodParameters[i];
				if (obj != null && !parameterInfo.ParameterType.IsAssignableFrom(obj.GetType()))
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x06000654 RID: 1620 RVA: 0x00015FB4 File Offset: 0x000141B4
		private static object ConvertValueTo(string value, Type parameterType)
		{
			object result = null;
			if (parameterType == typeof(string))
			{
				result = value;
			}
			else if (parameterType == typeof(int))
			{
				result = Convert.ToInt32(value);
			}
			else if (parameterType == typeof(float))
			{
				result = Convert.ToSingle(value);
			}
			return result;
		}

		// Token: 0x06000655 RID: 1621 RVA: 0x00016018 File Offset: 0x00014218
		public virtual void RefreshValues()
		{
		}

		// Token: 0x06000656 RID: 1622 RVA: 0x0001601C File Offset: 0x0001421C
		public static void RefreshPropertyAndMethodInfos()
		{
			ViewModel._cachedViewModelProperties.Clear();
			Assembly[] viewModelAssemblies = ViewModel.GetViewModelAssemblies();
			for (int i = 0; i < viewModelAssemblies.Length; i++)
			{
				List<Type> typesSafe = viewModelAssemblies[i].GetTypesSafe(null);
				for (int j = 0; j < typesSafe.Count; j++)
				{
					Type type = typesSafe[j];
					if (typeof(IViewModel).IsAssignableFrom(type) && typeof(IViewModel) != type)
					{
						ViewModel.DataSourceTypeBindingPropertiesCollection propertiesOfType = ViewModel.GetPropertiesOfType(type);
						ViewModel._cachedViewModelProperties[type] = propertiesOfType;
					}
				}
			}
		}

		// Token: 0x06000657 RID: 1623 RVA: 0x000160A8 File Offset: 0x000142A8
		private static Assembly[] GetViewModelAssemblies()
		{
			List<Assembly> list = new List<Assembly>();
			Assembly assembly = typeof(ViewModel).Assembly;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			list.Add(assembly);
			foreach (Assembly assembly2 in assemblies)
			{
				if (assembly2 != assembly)
				{
					AssemblyName[] referencedAssemblies = assembly2.GetReferencedAssemblies();
					for (int j = 0; j < referencedAssemblies.Length; j++)
					{
						if (referencedAssemblies[j].ToString() == assembly.GetName().ToString())
						{
							list.Add(assembly2);
							break;
						}
					}
				}
			}
			return list.ToArray();
		}

		// Token: 0x06000658 RID: 1624 RVA: 0x00016144 File Offset: 0x00014344
		private static ViewModel.DataSourceTypeBindingPropertiesCollection GetPropertiesOfType(Type t)
		{
			string name = t.Name;
			Dictionary<string, PropertyInfo> dictionary = new Dictionary<string, PropertyInfo>();
			Dictionary<string, MethodInfo> dictionary2 = new Dictionary<string, MethodInfo>();
			foreach (PropertyInfo propertyInfo in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				dictionary.Add(propertyInfo.Name, propertyInfo);
			}
			foreach (MethodInfo methodInfo in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (!dictionary2.ContainsKey(methodInfo.Name))
				{
					dictionary2.Add(methodInfo.Name, methodInfo);
				}
			}
			return new ViewModel.DataSourceTypeBindingPropertiesCollection(dictionary, dictionary2);
		}

		// Token: 0x040001CF RID: 463
		public static bool UIDebugMode;

		// Token: 0x040001D0 RID: 464
		private List<PropertyChangedEventHandler> _eventHandlers;

		// Token: 0x040001D1 RID: 465
		private List<PropertyChangedWithValueEventHandler> _eventHandlersWithValue;

		// Token: 0x040001D2 RID: 466
		private List<PropertyChangedWithBoolValueEventHandler> _eventHandlersWithBoolValue;

		// Token: 0x040001D3 RID: 467
		private List<PropertyChangedWithIntValueEventHandler> _eventHandlersWithIntValue;

		// Token: 0x040001D4 RID: 468
		private List<PropertyChangedWithFloatValueEventHandler> _eventHandlersWithFloatValue;

		// Token: 0x040001D5 RID: 469
		private List<PropertyChangedWithUIntValueEventHandler> _eventHandlersWithUIntValue;

		// Token: 0x040001D6 RID: 470
		private List<PropertyChangedWithColorValueEventHandler> _eventHandlersWithColorValue;

		// Token: 0x040001D7 RID: 471
		private List<PropertyChangedWithDoubleValueEventHandler> _eventHandlersWithDoubleValue;

		// Token: 0x040001D8 RID: 472
		private List<PropertyChangedWithVec2ValueEventHandler> _eventHandlersWithVec2Value;

		// Token: 0x040001D9 RID: 473
		private Type _type;

		// Token: 0x040001DA RID: 474
		private ViewModel.DataSourceTypeBindingPropertiesCollection _propertiesAndMethods;

		// Token: 0x040001DB RID: 475
		private static Dictionary<Type, ViewModel.DataSourceTypeBindingPropertiesCollection> _cachedViewModelProperties = new Dictionary<Type, ViewModel.DataSourceTypeBindingPropertiesCollection>();

		// Token: 0x020000EB RID: 235
		public interface IViewModelGetterInterface
		{
			// Token: 0x060007BC RID: 1980
			bool IsValueSynced(string name);

			// Token: 0x060007BD RID: 1981
			Type GetPropertyType(string name);

			// Token: 0x060007BE RID: 1982
			object GetPropertyValue(string name);

			// Token: 0x060007BF RID: 1983
			void OnFinalize();
		}

		// Token: 0x020000EC RID: 236
		public interface IViewModelSetterInterface
		{
			// Token: 0x060007C0 RID: 1984
			void SetPropertyValue(string name, object value);

			// Token: 0x060007C1 RID: 1985
			void OnFinalize();
		}

		// Token: 0x020000ED RID: 237
		private class DataSourceTypeBindingPropertiesCollection
		{
			// Token: 0x1700010A RID: 266
			// (get) Token: 0x060007C2 RID: 1986 RVA: 0x00019627 File Offset: 0x00017827
			// (set) Token: 0x060007C3 RID: 1987 RVA: 0x0001962F File Offset: 0x0001782F
			public Dictionary<string, PropertyInfo> Properties { get; set; }

			// Token: 0x1700010B RID: 267
			// (get) Token: 0x060007C4 RID: 1988 RVA: 0x00019638 File Offset: 0x00017838
			// (set) Token: 0x060007C5 RID: 1989 RVA: 0x00019640 File Offset: 0x00017840
			public Dictionary<string, MethodInfo> Methods { get; set; }

			// Token: 0x060007C6 RID: 1990 RVA: 0x00019649 File Offset: 0x00017849
			public DataSourceTypeBindingPropertiesCollection(Dictionary<string, PropertyInfo> properties, Dictionary<string, MethodInfo> methods)
			{
				this.Properties = properties;
				this.Methods = methods;
			}
		}
	}
}
