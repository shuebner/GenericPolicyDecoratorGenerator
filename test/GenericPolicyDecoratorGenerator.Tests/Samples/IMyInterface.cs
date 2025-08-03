using System.Threading.Tasks;

namespace SvSoft.Analyzers.GenericDecoratorGeneration.Samples;
interface IMyInterface
{
    Task<string> GetStuffAsync(int arg1, string arg2);
}
