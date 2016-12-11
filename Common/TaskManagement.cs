using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Common {
   public static class TaskManagement {

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Forget(this Task task) {
         task.ConfigureAwait(false);
      }
   }
}
