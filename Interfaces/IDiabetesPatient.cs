using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Interfaces {

   public interface IDiabetesPatient : IActor {
      Task InitializeCooperativeNLMS(int coefficientsNum, double coefficientsStep, double coefficientsNormalizer, long[] cluster);
      Task RunAStep(object state);
      Task SendWeights(long neighbor, long iterationId, double[] theirWeights);

      Task<double[]> NonCooperativeNLMS(int coefficientsNum, double stepNum, double normalizerNum);
   }
}
