﻿# WinUI Depdency Injection Frame Navigate

This is a simple sample app which demonstrates how to use the WinUI Frame.Navigate() method with Dependency Injection.

## How it works
WinUI frame.Navigate method calls the IXamlType.ActivateInstance() method to create a new instance of a page. To use a SerivceProvider to create a page
the IXamlType.ActivateInstance() method must be overriden.
Behind the scences a source generator generates XamlTypeInfo.g.cs where some type info stuff is implemented and the App class implements the IXamlMetadataProvider interface.
In the source we can see that the overriding the ActivateInstance method is directly not possible. 
The App class which is generated by the source generator implements the IXamlMetadataProvider interface implicitly so it is possible to 'override' the IXamlMetadataProvider interface
methods with explicit implementations. Below you can see the required sections to understand how this works.\
*Note: the ```XamlTypeInfo.g.cs``` is located in the obj\\{platform\}\Debug\\{targetFramework\}\\{runtime\} folder.*

```XamlTypeInfo.g.cs```:
```csharp
namespace WinUI.DI.FrameNavigate
{
    public partial class App : global::Microsoft.UI.Xaml.Markup.IXamlMetadataProvider
    {
        // ...
        private global::WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo.XamlMetaDataProvider _AppProvider { get; }

        public global::Microsoft.UI.Xaml.Markup.IXamlType GetXamlType(global::System.Type type)
        {
            return _AppProvider.GetXamlType(type);
        }
        // ...
    }
}
namespace WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo
{
    public sealed class XamlMetaDataProvider : global::Microsoft.UI.Xaml.Markup.IXamlMetadataProvider
    {
        // ...
        private global::WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo.XamlTypeInfoProvider Provider { get; }

        public global::Microsoft.UI.Xaml.Markup.IXamlType GetXamlType(global::System.Type type)
        {
            return Provider.GetXamlType(type);
        }
    }

    internal partial class XamlTypeInfoProvider
    {
        // ...
        global::System.Type[] _typeTable = null;
        private void InitTypeTables()
        {
            // ...
            _typeTable[22] = typeof(global::WinUI.DI.FrameNavigate.Views.MainPage);
            // ...
        }

        private object Activate_22_MainPage() { return new global::WinUI.DI.FrameNavigate.Views.MainPage(); }

        private global::Microsoft.UI.Xaml.Markup.IXamlType CreateXamlType(int typeIndex)
        {
            // ...
            global::WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo.XamlSystemBaseType xamlType = null;
            global::System.Type type = _typeTable[typeIndex];
            switch (typeIndex)
            {
                // ...
                case 22:
                    userType = new global::WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo.XamlUserType(this, typeName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"));
                    userType.Activator = Activate_22_MainPage;
                    userType.AddMemberName("ViewModel");
                    userType.SetIsLocalType();
                    xamlType = userType;
                    break;
                // ...
            }
            return xamlType;
        }
    }

    internal class XamlSystemBaseType : global::Microsoft.UI.Xaml.Markup.IXamlType
    {
        // ...
        public XamlSystemBaseType(string fullName, global::System.Type underlyingType)
        {
            // ...
        }

        public global::System.Type UnderlyingType { get; }
        // ...
    }
    
    internal delegate object Activator();
    // ...

    internal class XamlUserType : global::WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo.XamlSystemBaseType
        , global::Microsoft.UI.Xaml.Markup.IXamlType
    {
        public XamlUserType(global::WinUI.DI.FrameNavigate.WinUI_DI_FrameNavigate_XamlTypeInfo.XamlTypeInfoProvider provider, string fullName, global::System.Type fullType, global::Microsoft.UI.Xaml.Markup.IXamlType baseType)
            :base(fullName, fullType)
        {
            // ...
        }

        // ...
        override public object ActivateInstance()
        {
            return Activator(); 
        }
        // ...
        public Activator Activator { get; set; }
        // ...
    }
}
```
[`App.DI.cs`](https://github.com/gabor-budai/WinUI-DI-FrameNavigate/blob/master/WinUI.DI.FrameNavigate/App.DI.cs):
```csharp
namespace WinUI.DI.FrameNavigate;
file class XamlMetadataProvider : IXamlMetadataProvider
{
    // ...
    // The wrapped provider, initialized in the constructor.
    private readonly IXamlMetadataProvider _appProvider;
    // The App's ServiceProvider.
    private IServiceProvider ServiceProvider { get; }

    private void RetargetUserTypeActivator(IXamlType? xamlType)
    {
        // ...
        // The implementation is equivalent to this.
        // xamlType.Activator = () => ServiceProvider.GetService(xamlType.UnderlyingType);
    }

    public IXamlType GetXamlType(Type type)
    {
        var xamlType = _appProvider.GetXamlType(type);
        RetargetUserTypeActivator(xamlType);
        return xamlType;
    }

    public IXamlType GetXamlType(string fullName)
    {
        var xamlType = _appProvider.GetXamlType(fullName);
        RetargetUserTypeActivator(xamlType);
        return xamlType;
    }

    public XmlnsDefinition[] GetXmlnsDefinitions() => _appProvider.GetXmlnsDefinitions();
    // ...
}

partial class App : IXamlMetadataProvider
{
    // ...
    // The custom provider.
    private IXamlMetadataProvider _AppUserProvider { get; }
    IXamlType IXamlMetadataProvider.GetXamlType(Type type) => _AppUserProvider.GetXamlType(type);
    IXamlType IXamlMetadataProvider.GetXamlType(string fullName) => _AppUserProvider.GetXamlType(fullName);
    // ...
}
```
