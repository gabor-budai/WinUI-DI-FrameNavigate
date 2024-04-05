using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Markup;
using System.Diagnostics;
using System.Linq.Expressions;

namespace WinUI.DI.FrameNavigate;

file class XamlMetadataProvider : IXamlMetadataProvider
{
    private readonly App _app;
    private readonly IXamlMetadataProvider _appProvider;
    private readonly Type _xamlUserType;
    private readonly Type _activatorDelegateType;
    private readonly Action<object, object> _setActivator;
    private readonly Func<object, object> _getUnderlyingType;
    private IServiceProviderIsService? _isService;

    private IServiceProvider ServiceProvider
    {
        get
        {
            Debug.Assert(_app.Host is not null, "The Host is accessed before it's built.");
            return _app.Host.Services;
        }
    }

    private IServiceProviderIsService IsService => _isService ??= ServiceProvider.GetRequiredService<IServiceProviderIsService>();

    // WinUI has an undocumented 'feature', it has a type activator 'service', I think it provides interpolation between the managed and
    // unmanaged code. This type activator has two major flaws, it does not support parameterless constructors
    // (it can be resolved by the inversion of control pattern) and frame navigation does not allow passing an instance of the page. So DI
    // cannot be used to instantiate a page and with TemplateStudio it can be misleading, because TemplateStudio registers all pages as
    // services. Behind the scenes VS generates XamlTypeInfo.g.cs. and it will have some helper classes:
    // XamlMetaDataProvider - implements the Microsoft.UI.Xaml.Markup.IXamlMetadataProvider interface
    // XamlTypeInfoProvider - generated type 'lookup'
    // XamlSystemBaseType - type for the system types and the base of the XamlUserType
    // XamlUserType - type for the user defined types
    // We need to focus on the XamlTypeInfoProvider and XamlUserType. A snippet from XamlTypeInfoProvider:
    // private global::Microsoft.UI.Xaml.Markup.IXamlType CreateXamlType(int typeIndex)
    // {
    //     global::System.Type type = _typeTable[typeIndex]; // This will be the UnderlyingType
    //     switch (typeIndex)
    //     {
    //         // ...      
    //         case 117: // generated type index   
    //             //                                                                         type := UnderlyingType                                                                         
    //             userType = new global::{ns}.{ns}_XamlTypeInfo.XamlUserType(this, typeName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"));
    //             // Activator is responsible for creating the instance of the page, if any parameters are provided (in ctor), the
    //             // Activator setter will be removed and it will cause a null reference exception when frame tries to navigate to the
    //             // page.
    //             userType.Activator = Activate_117_TempPage;
    //             userType.SetIsLocalType();
    //             xamlType = userType;
    //             break;
    //         // ...
    //     }
    // }
    // We can see for the pages the generator uses XamlUserType and the Activator is responsible for creating the instance of the page. This
    // source generator also generates a partial class for the App class. In this partial implementation, App has the _AppProvider property,
    // whose type is the XamlMetaDataProvider helper class. App also implements the IXamlMetadataProvider interface and its  methods will
    // invoke the helper class methods. Since the App class is partial, the _AppProvider property would be accessible.  However, the source
    // generator works strangely, if the _AppProvider property (or anything from the generated source) was referenced, the generator would
    // fail. It's not a problem but we must use reflection to access these properties. Fortunately, the generator implements the
    // IXamlMetadataProvider interface implicitly, so we can create a partial class that implements the interface explicitly and we can do
    // as we want.
    public XamlMetadataProvider(App app)
    {
        // Do not initialize services here, as they may not be available yet. If a UI element is created before the host is built, it might
        // call the provider. I encountered this 'issue' with a ScrollView within a logger (can be instantiated before the host is built),
        // when new ScrollView() was called constructor calls GetXamlType(). However, the XamlType won't be equal to XamlUserType, so it won't
        // cause any issues. When the XamlType is a XamlUserType, this issue still exists, and you must use a lazy pattern.

        _app = app;
        var appType = app.GetType();
        var ns = appType.Namespace!;
        var xamlTypeInfoNamespace = $"{ns}.{ns.Replace('.', '_')}_XamlTypeInfo";
        
        // The user type descriptor, it's used to decide whether the type is a user type or not.
        _xamlUserType = Type.GetType($"{xamlTypeInfoNamespace}.XamlUserType")!;
        // The delegate type, since Func<T> is not convertible to a delegate type, Activator.CreateDelegate is used.
        _activatorDelegateType = Type.GetType($"{xamlTypeInfoNamespace}.Activator")!;

        Debug.Assert(!(_xamlUserType is null || _activatorDelegateType is null), "Something went wrong");

        _appProvider = (IXamlMetadataProvider)CreateGetter(appType, "_AppProvider")(app);

        // It's used to decide whether the type is a service or not.
        _getUnderlyingType = CreateGetter(_xamlUserType, "UnderlyingType");
        // It's used to redirect the Activator to the service provider.
        _setActivator = CreateSetter(_xamlUserType, "Activator");

        Debug.Assert(!(_appProvider is null || _getUnderlyingType is null || _setActivator is null), "Something went wrong");
    }

    private void RetargetUserTypeActivator(IXamlType? xamlType)
    {
        // If xamlType isn't a user type, we don't need to do anything.
        if (xamlType?.GetType() != _xamlUserType) return;

        var underlyingType = (Type)_getUnderlyingType(xamlType);

        // If underlyingType isn't a service type, we don't need to do anything.
        if (!IsService.IsService(underlyingType)) return;

        // It captures the service provider and the underlying type.
        var func = () => ServiceProvider.GetService(underlyingType);

        // Here we redirect the Activator.
        var d = Delegate.CreateDelegate(_activatorDelegateType, func.Target, func.Method);
        _setActivator(xamlType, d);
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

    private static Func<object, object> CreateGetter(Type type, string propertyName)
    {
        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var convertedInstance = Expression.Convert(instanceParameter, type);
        var property = Expression.Property(convertedInstance, propertyName);
        var convert = Expression.Convert(property, typeof(object));

        var lambda = Expression.Lambda<Func<object, object>>(convert, instanceParameter);
        return lambda.Compile();
    }

    private static Action<object, object> CreateSetter(Type type, string propertyName)
    {
        var propertyInfo = type.GetProperty(propertyName)!;
        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var valueParameter = Expression.Parameter(typeof(object), "value");

        var convertedInstance = Expression.Convert(instanceParameter, type);
        var convertedValue = Expression.Convert(valueParameter, propertyInfo.PropertyType);
        var property = Expression.Property(convertedInstance, propertyName);
        var assign = Expression.Assign(property, convertedValue);

        var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParameter, valueParameter);
        return lambda.Compile();
    }
}

partial class App : IXamlMetadataProvider
{
    private IXamlMetadataProvider? __appUserProvider;
    private IXamlMetadataProvider _AppUserProvider => __appUserProvider ??= new XamlMetadataProvider(this);
    IXamlType IXamlMetadataProvider.GetXamlType(Type type) => _AppUserProvider.GetXamlType(type);
    IXamlType IXamlMetadataProvider.GetXamlType(string fullName) => _AppUserProvider.GetXamlType(fullName);
    // Not used to redirect types, but it's required, because the generator will fail if it's not overridden.
    XmlnsDefinition[] IXamlMetadataProvider.GetXmlnsDefinitions() => _AppUserProvider.GetXmlnsDefinitions();
}
