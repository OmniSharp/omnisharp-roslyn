using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.ApplicationModels;
using Microsoft.AspNet.Mvc.ModelBinding;

namespace OmniSharp.Settings
{
    public class FromBodyApplicationModelConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                foreach (var action in controller.Actions)
                {
                    foreach (var parameter in action.Parameters)
                    {
                        if (parameter.BinderMetadata is IBinderMetadata || ValueProviderResult.CanConvertFromString(parameter.ParameterInfo.ParameterType))
                        {
                            // behavior configured or simple type so do nothing
                        }
                        else
                        {
                            // Complex types are by-default from the body.
                            parameter.BinderMetadata = new FromBodyAttribute();
                        }
                    }
                }
            }
        }
    }
}