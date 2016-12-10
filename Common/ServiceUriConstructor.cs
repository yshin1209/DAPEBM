using System;
using System.Fabric;

namespace Common {
   public static class ServiceUriConstructor {

      public static Uri ServiceUriFor(string serviceName) {
         return new Uri($"{FabricRuntime.GetActivationContext().ApplicationName}/{serviceName}");
      }
   }
}