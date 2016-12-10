using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using Interfaces;

namespace DiabetesPatient {

   [ActorService(Name = "DiabetesPatient")]
   [StatePersistence(StatePersistence.Persisted)]
   internal class DiabetesPatient : Actor, IDiabetesPatient {
      public DiabetesPatient(ActorService actorService, ActorId actorId)
          : base(actorService, actorId) {
      }

      protected override Task OnActivateAsync() {
         ActorEventSource.Current.ActorMessage(this, "Actor activated.");
         return Task.FromResult(true);
      }

      Task<string> IDiabetesPatient.LMS(int mN, int wN, double sN, double fN) {
         /* Constants */
         int measurementsNum = mN; // 1000
         int weightsNum = wN; // 5
         double step = sN; // 0.1
         double filterSeed = fN; // 0.5

         double[] filterC = new double[weightsNum];
         double[] weights = new double[weightsNum];
         double[] signals = new double[measurementsNum];
         double[] measurements = new double[measurementsNum];

         /* Filter and Signal initializations */
         for (int l = 0; l < weightsNum ; l++) {
            filterC[l] = Math.Pow(filterSeed, weightsNum - l - 1);
         }

         var random = new Random();
         for (int l = 0; l < measurementsNum; l++) {
            measurements[l] = random.NextDouble();

            for (int u = 0; u < weightsNum; u++) {
               if (l - u >= 0) {
                  signals[l] += measurements[l - u] * filterC[u];
               }
            }
         }

         double[] range = new double[weightsNum];
         double estimatedSignal = 0;
         double error = 0;
         for (int l = 0; l < measurementsNum; l++) {

            // Input Range
            for (int u = l; u > l - weightsNum; u--) {
               if (u >= 0) {
                  range[weightsNum + (u - l) - 1] = measurements[u];
               } else {
                  break;
               }
            }

            estimatedSignal = 0;
            for (int u = 0; u < weightsNum; u++) {
               estimatedSignal += weights[u] * range[u];
            }

            error = signals[l] - estimatedSignal;

            for (int u = 0; u < weightsNum; u++) {
               weights[u] = weights[u] + (step * error * range[u]);
            }

         }
         ActorEventSource.Current.ActorMessage(this, "After looping.");

         string message = "Comparison between filter and weights:\n";
         for (int u = 0; u < weightsNum; u++) {
            message += $"\t{u + 1} => {filterC[weightsNum - u - 1]} | {weights[u]}\n";
         }
         ActorEventSource.Current.ActorMessage(this, message);

         return Task.FromResult(message);
      }

   }
}
