using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Interfaces {

   public interface IDiabetesPatient : IActor {
      Task<string> LMS(int mN, int wN, double sN, double fN);
   }
}
