using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using Interfaces;
using static Common.ServiceUriConstructor;
using static Common.TaskManagement;

namespace DiabetesPatient {

   [ActorService(Name = "DiabetesPatient")]
   [StatePersistence(StatePersistence.Persisted)]
   internal class DiabetesPatient : Actor, IDiabetesPatient {
      public DiabetesPatient(ActorService actorService, ActorId actorId)
          : base(actorService, actorId) {
      }

      private double[] myData = new double[0];
      protected override Task OnActivateAsync() {
         ActorId myActorId = this.GetActorId();
         long myId = (myActorId.Kind == ActorIdKind.Long ? myActorId.GetLongId() : myActorId.GetHashCode());
         if (AllData.From.ContainsKey(myId)) {
            myData = AllData.From[myId];
            Console.WriteLine($"Loaded data from dictionary: {myId}");
         }
         return base.OnActivateAsync();
      }

      protected override Task OnDeactivateAsync() {
         if (IterationTimer != null) {
            UnregisterTimer(IterationTimer);
            IterationTimer = null;
         }
         return base.OnDeactivateAsync();
      }

      private double myStep;
      private double myNormalizer;
      async Task IDiabetesPatient.InitializeCooperativeNLMS(int coefficientsNum, double coefficientsStep, double coefficientsNormalizer, long[] cluster) {
         var neighbors = new Neighbors(true);
         ActorId myActorId = this.GetActorId();
         long myId = (myActorId.Kind == ActorIdKind.Long ? myActorId.GetLongId() : myActorId.GetHashCode());

         for (int l = 0; l < cluster.Length; l++) {
            if (cluster[l] != myId) {
               neighbors[cluster[l]] = new NeighborDetails(0);
               for (long u = 0; u < coefficientsNum; u++) {
                  neighbors[cluster[l]][0].Add(0);
               }
            }
         }

         var coefficients = new double[coefficientsNum];
         for (int l = 0; l < coefficientsNum; l++) {
            coefficients[l] = 0;
         }

         myStep = coefficientsStep;
         myNormalizer = coefficientsNormalizer;
         await this.StateManager.AddOrUpdateStateAsync<Neighbors>("neighbors", neighbors, (key, value) => neighbors);
         await this.StateManager.AddOrUpdateStateAsync<long>("currentIteration", 1, (key, value) => 1);
         await this.StateManager.AddOrUpdateStateAsync<double[]>("coefficients", coefficients, (key, value) => coefficients);

         ActorEventSource.Current.ActorMessage(this, $"Data initialized for actor: {myId}");
         return;
      }

      private IActorTimer IterationTimer;
      async Task IDiabetesPatient.RunAStep(object state) {
         var currentIteration = await this.StateManager.GetStateAsync<long>("currentIteration");
         var neighbors = await this.StateManager.GetStateAsync<Neighbors>("neighbors");
         var coefficients = await this.StateManager.GetStateAsync<double[]>("coefficients");

         if ((!neighbors.AllHaveDataFor(currentIteration) || currentIteration >= myData.PossibleIterations()) && IterationTimer != null) {
            UnregisterTimer(IterationTimer);
            IterationTimer = null;
         }

         var updateIteration = this.StateManager.SetStateAsync("currentIteration", currentIteration + 1);

         // Input Range
         double[] range = new double[coefficients.Length];
         for (long l = currentIteration; l > currentIteration - coefficients.Length; l--) {
            if (l >= 1) {
               range[coefficients.Length + (l - currentIteration) - 1] = myData.AsMeasurement(l - 1);
            } else {
               break;
            }
         }

         double estimatedSignal = 0;
         double normalizedStep = myNormalizer;
         for (int l = 0; l < coefficients.Length; l++) {
            estimatedSignal += coefficients[l] * range[l];
            normalizedStep += range[l] * range[l];
         }

         normalizedStep = myStep / normalizedStep;
         double error = myData.AsSignal(currentIteration - 1) - estimatedSignal;

         var weightsForFile = "";
         double[] psi = new double[coefficients.Length];
         for (int l = 0; l < coefficients.Length; l++) {
            psi[l] = coefficients[l] + (normalizedStep * error * range[l]);
            weightsForFile += $",{coefficients[l]}";
            coefficients[l] = psi[l];
         }

         for (int l = 0; l < coefficients.Length; l++) {
            foreach (var neighbor in neighbors) {
               coefficients[l] += neighbor.Value[currentIteration - 1][l];
            }
            coefficients[l] /= neighbors.Count + 1;
         }

         var updateCoefficients = this.StateManager.SetStateAsync("coefficients", coefficients);

         IDiabetesPatient neighborProxy;
         ActorId myActorId = this.GetActorId();
         long myId = (myActorId.Kind == ActorIdKind.Long ? myActorId.GetLongId() : myActorId.GetHashCode());
         foreach (var neighbor in neighbors) {
            neighborProxy = ActorProxy.Create<IDiabetesPatient>(new ActorId(neighbor.Key), ServiceUriFor("DiabetesPatient"));
            neighborProxy.SendWeights(myId, currentIteration, psi).Forget();
         }

         using (StreamWriter csvWriter = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), $"{myId}_coopLMS.csv"), true)) {
            csvWriter.Write($"{currentIteration},{myData.AsSignal(currentIteration - 1)},{estimatedSignal}");
            csvWriter.WriteLine(weightsForFile);
         }

         await updateCoefficients;
         await updateIteration;

         ActorEventSource.Current.ActorMessage(this, $"Ran iteration[{currentIteration}] for actor: {myId}");
         return;
      }

      async Task IDiabetesPatient.SendWeights(long neighbor, long iterationId, double[] theirWeights) {
         var nextIteration = await this.StateManager.GetStateAsync<long>("currentIteration");
         var neighbors = await this.StateManager.GetStateAsync<Neighbors>("neighbors");

         ActorEventSource.Current.ActorMessage(this, $"Call Params: neighbor => {neighbor}, iter => {iterationId}");

         neighbors[neighbor][iterationId] = new List<double>(theirWeights);
         await this.StateManager.SetStateAsync("neighbors", neighbors);

         if (nextIteration == (iterationId + 1) && nextIteration <= myData.PossibleIterations() && neighbors.AllHaveDataFor(iterationId)) {
            if (IterationTimer == null) {
               IterationTimer = RegisterTimer(((IDiabetesPatient)this).RunAStep, null, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(500));
            }
         }

         return;
      }

      Task<double[]> IDiabetesPatient.NonCooperativeNLMS(int coefficientsNum, double stepNum, double normalizerNum) {
         double[] coefficients = new double[coefficientsNum];
         double[] range = new double[coefficientsNum];
         double estimatedSignal = 0;
         double error = 0;
         double normalizedStep = 0;

         ActorId myActorId = this.GetActorId();
         long myId = (myActorId.Kind == ActorIdKind.Long ? myActorId.GetLongId() : myActorId.GetHashCode());
         using (StreamWriter csvWriter = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), $"{myId}_non-coopLMS.csv"), true)) {

            for (int l = 0; l < myData.PossibleIterations(); l++) {

               for (int u = l; u > l - coefficientsNum; u--) {
                  if (u >= 0) {
                     range[coefficientsNum + (u - l) - 1] = myData.AsMeasurement(u);
                  } else {
                     break;
                  }
               }

               estimatedSignal = 0;
               normalizedStep = normalizerNum;
               for (int u = 0; u < coefficientsNum; u++) {
                  estimatedSignal += coefficients[u] * range[u];
                  normalizedStep += range[u] * range[u];
               }
               normalizedStep = stepNum / normalizedStep;

               error = myData.AsSignal(l) - estimatedSignal;

               csvWriter.Write($"{l + 1},{myData.AsSignal(l)},{estimatedSignal}");
               for (int u = 0; u < coefficientsNum; u++) {
                  csvWriter.Write($",{coefficients[u]}");
                  coefficients[u] = coefficients[u] + (normalizedStep * error * range[u]);
               }
               csvWriter.WriteLine();

            }
         }

         return Task.FromResult(coefficients);
      }

   }

   internal static class ArrayExtensions {
      public static elementType AsMeasurement<elementType>(this elementType[] array, long index) {
         return array[index];
      }

      public static elementType AsSignal<elementType>(this elementType[] array, long index) {
         return array[index + 1];
      }

      public static int PossibleIterations<elementType>(this elementType[] array) {
         return array.Length - 1;
      }
   }
}
