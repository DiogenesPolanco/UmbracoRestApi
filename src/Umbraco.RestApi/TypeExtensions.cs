using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Umbraco.RestApi
{
    internal static class TypeExtensions
    {
        internal static Type GetEnumeratedType(this Type type)
        {
            if (typeof(IEnumerable).IsAssignableFrom(type) == false)
                return null;

            // provided by Array
            var elType = type.GetElementType();
            if (null != elType) return elType;

            // otherwise provided by collection
            var elTypes = type.GetGenericArguments();
            if (elTypes.Length > 0) return elTypes[0];

            // otherwise is not an 'enumerated' type
            return null;
        }
    }
}
