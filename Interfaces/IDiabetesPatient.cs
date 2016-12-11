using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Interfaces {

   public interface IDiabetesPatient : IActor {
      Task PrepareData(long mN, int wN, double sN, double fN);
      Task Initialize(long[] cluster);
      Task RunAStep();
      Task SendWeights(long neighbor, long iterationId, double[] theirWeights);

      Task<string> LMS(int mN, int wN, double sN, double fN);
   }
}
