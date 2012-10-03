using System;
using System.Diagnostics;
using N2.Persistence;
using N2.Persistence.Finder;
using N2.Plugin.Scheduling;
using N2.Security;
using N2.Engine;

namespace N2.Edit.AutoPublish
{
    [ScheduleExecution(30, TimeUnit.Seconds)]
    public class PublishScheduledAction : ScheduledAction
    {
        IVersionManager Versioner { get { return Engine.Resolve<IVersionManager>(); } }
        IPersister Persister { get { return Engine.Resolve<IPersister>(); } }
		IItemFinder Finder { get { return Engine.Resolve<IItemFinder>(); } }
		ISecurityManager Security { get { return Engine.SecurityManager; } }

		Logger<PublishScheduledAction> logger;

        public override void Execute()
        {
            if (Debugger.IsAttached)
                return;

            var scheduledForAutoPublish = Finder
				.Where.Detail("FuturePublishDate").Lt(DateTime.Now)
				.PreviousVersions(VersionOption.Include).Select();
			for (int i = 0; i < scheduledForAutoPublish.Count; i++)
			{
                // Get the relevant versions
				ContentItem scheduledVersion = scheduledForAutoPublish[i];
				ContentItem masterVersion = scheduledVersion.VersionOf;
				logger.InfoFormat("Publishing scheduled item {0}", scheduledVersion);
                // Removing the DelayPublishingUntil Date so that it won't get picked up again
                scheduledVersion["FuturePublishDate"] = null;

				try
				{
					Security.ScopeEnabled = false;
					if (masterVersion == null)
						Persister.Save(scheduledVersion);
					else
						Versioner.ReplaceVersion(masterVersion, scheduledVersion, true);
				}
				finally
				{
					Security.ScopeEnabled = true;
				}
            }

			var implicitAutoPublish = Finder
				.Where.Published.Le(Utility.CurrentTime())
				.And.State.Eq(ContentState.Waiting)
				.Select();
			for (int i = 0; i < implicitAutoPublish.Count; i++)
			{
				try
				{
					Security.ScopeEnabled = false;
				// saving the master version for auto-publish will be eventually become published without this, but we want to update the state
				var item = implicitAutoPublish[i];
					logger.InfoFormat("Implicitly publishing item {0}", item);
				item.State = ContentState.Published;
				Persister.Save(item);
        }
        finally
        {
          Security.ScopeEnabled = true;
        }
      }
        }
    }
}
