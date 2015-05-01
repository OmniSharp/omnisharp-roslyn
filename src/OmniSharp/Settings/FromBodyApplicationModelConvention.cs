using Microsoft.AspNet.Mvc.ApplicationModels;
using Microsoft.AspNet.Mvc.ModelBinding;
using System.Linq;

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
                        if (parameter.BindingInfo?.BindingSource != null ||
                            parameter.Attributes.OfType<IBindingSourceMetadata>().Any() ||
                            ValueProviderResult.CanConvertFromString(parameter.ParameterInfo.ParameterType))
                        {
                            // behavior configured or simple type so do nothing
                        }
                        else
                        {
                            // Complex types are by-default from the body.
                            parameter.BindingInfo = parameter.BindingInfo ?? new BindingInfo();
                            parameter.BindingInfo.BindingSource = BindingSource.Body;
                        }
                    }
                }
            }
        }
    }
}