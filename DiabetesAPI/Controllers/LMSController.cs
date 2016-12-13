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
      //private ActorId ForwardActorId = ActorId.CreateRandom();

      [HttpGet("[action]/{cN:int}/{sN}/{gN}/{nN:int}/{iA:long}")]
      public async Task<IActionResult> CNLMS(int cN, double sN, double gN, int nN, long iA) {
         var result = "Can't run";

         if (nN > 1) {
            IDiabetesPatient actor;

            var cluster = new long[nN];
            for (var l = 0; l < cluster.Length; l++) {
               cluster[l] = iA + l;
            }

            for (var l = 0; l < cluster.Length; l++) {
               actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(cluster[l]), ServiceUriFor("DiabetesPatient"));
               await actor.InitializeCooperativeNLMS(cN, sN, gN, cluster);
            }

            for (var l = 0; l < cluster.Length; l++) {
               actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(cluster[l]), ServiceUriFor("DiabetesPatient"));
               actor.RunAStep(null).Forget();
            }

            result = "It's running.";
         }
         
         return Ok(result);
      }

      [HttpGet("[action]/{cN:int}/{sN}/{gN}/{iA:long}")]
      public async Task<IActionResult> NCNLMS(int cN, double sN, double gN, long iA) {
         var actor = ActorProxy.Create<IDiabetesPatient>(new ActorId(iA), ServiceUriFor("DiabetesPatient"));
         var result = await actor.NonCooperativeNLMS(cN, sN, gN);
         return Ok(result);
      }
   }
}
