using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Shared.Constants
{
    public static class CacheKeys
    {
        public const string USER_ORDERS = "user_orders_{0}";
        public const int DEFAULT_TTL_MINUTES = 2;
    }
}
