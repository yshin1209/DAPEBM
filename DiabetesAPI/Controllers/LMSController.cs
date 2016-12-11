using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Interfaces;
using static Common.ServiceUriConstructor;
using static Common.TaskManagement;
using System.Text;

namespace DiabetesAPI.Controllers {

   [Route("api")]
   public class LMSController : Controller {
      private ActorId ForwardActorId = ActorId.CreateRandom();

      [HttpGet("[action]/{mN:long}/{wN:int}/{sN}/{fN}/{nN:int}")]
      public async Task<IActionResult> DLMS(long mN, int wN, double sN, double fN, int nN) {
         var result = "Can't run";

         if (nN > 1) {
            IDiabetesPatient actor;
            var initialActor = (new Random()).Next(0, 999999);

            actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(initialActor), ServiceUriFor("DiabetesPatient"));
            await actor.PrepareData(mN, wN, sN, fN);
            var actor2 = ActorProxy.Create<IDiabetesPatient>(new ActorId(initialActor+1), ServiceUriFor("DiabetesPatient"));
            var cluster = new long[2] { initialActor, initialActor + 1 };
            await actor.Initialize(cluster);
            await actor2.Initialize(cluster);
            actor.RunAStep().Forget();
            actor2.RunAStep().Forget();

            //var cluster = new int[nN];
            //for (var l = 0; l < cluster.Length; l++) {
            //   cluster[l] = initialActor + l;
            //}

            //for (var l = 0; l < cluster.Length; l++) {
            //   actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(cluster[l]), ServiceUriFor("DiabetesPatient"));
            //   await actor.Initialize(cluster);
            //}

            //for (var l = 0; l < cluster.Length; l++) {
            //   actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(cluster[l]), ServiceUriFor("DiabetesPatient"));
            //   actor.RunAStep().Forget();
            //}

            result = "It's running.";
         }
         
         return Ok(result);
      }

      [HttpGet("[action]")]
      public Task<IActionResult> Test() {
         var result = new StringBuilder();
         List<ActorId> actors = new List<ActorId> {
            new ActorId(93),
            new ActorId(65),
            new ActorId(93)
         };

         foreach (var actor in actors) {
            result.AppendLine($"\nAn Actor:");
            result.AppendLine($"\t{nameof(actor.Kind)} => {actor.Kind}");
            result.AppendLine($"\t{nameof(actor.GetHashCode)} => {actor.GetHashCode()}");
            result.AppendLine($"\t{nameof(actor.GetLongId)} => {actor.GetLongId()}");
            result.AppendLine($"\t{nameof(actor.ToString)} => {actor.ToString()}");
         }

         result.AppendLine($"\n Are the first two Ids equal?: {actors[0] == actors[1]}");
         result.AppendLine($"What about first and third one?: {actors[0] == actors[2]}");

         IActionResult fResult = Ok(result.ToString());
         return Task.FromResult(fResult);
      }

      [HttpGet("[action]/{mN:int}/{wN:int}/{sN}/{fN}")]
      public async Task<IActionResult> LMS(int mN, int wN, double sN, double fN) {
         var actor = ActorProxy.Create<IDiabetesPatient>(ForwardActorId, ServiceUriFor("DiabetesPatient"));
         var result = await actor.LMS(mN, wN, sN, fN);
         return Ok(result);
      }
   }
}
