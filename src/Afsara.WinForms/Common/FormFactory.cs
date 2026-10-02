using Microsoft.Extensions.DependencyInjection;

namespace Afsara.WinForms.Common;

public class FormFactory
{
    private readonly IServiceProvider _provider;

    public FormFactory(IServiceProvider provider) => _provider = provider;

    public T Create<T>(params object[] args) where T : Form =>
        ActivatorUtilities.CreateInstance<T>(_provider, args);
}
