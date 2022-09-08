using TrackPlanner.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrackPlanner.Data.RestSymbols;
using TrackPlanner.Shared;

namespace TrackPlanner.WebUI.Client
{
    public sealed class PlanWorker
    {
        private readonly RestClient rest;
        private readonly DraftHelper draftHelper;

        public PlanWorker( RestClient rest )
        {
            this.rest = rest;
            this.draftHelper = new DraftHelper(new ApproximateCalculator());
        }
        public async Task<( string? failure, TrackPlan? plan)>  GetPlanAsync(PlanRequest request,bool calcReal,CancellationToken token)
        {
            TrackPlan? plan;
            
            if (calcReal)
            {
                Console.WriteLine("Sending plan request");
                 (string? failure, plan) = await rest.PutAsync<TrackPlan>(Url.Combine(Program.Configuration.PlannerServer, Routes.Planner, Methods.Put_ComputeTrack),
                    request, token).ConfigureAwait(false);
                 if (failure != null)
                     return (failure, null);
                 
                 Console.WriteLine($"DEBUG, we have some auto anchors {plan!.Legs.Any(it => it.AutoAnchored)}");
            }
            else
            {
                plan = this.draftHelper.BuildDraftPlan(request);
            }

            return (null,plan);
        }

    }
}