using System;
using CadenceClub.Core;
using Template.Core.Save;
using Template.Game.Boot;
using Template.Game.Flow;
using Template.Infra;
using UnityEngine;

namespace CadenceClub
{
    /// <summary>
    /// The club's state (<see cref="ClubSave"/>, kept inside the template's save file) and the master data
    /// (Resources/MasterData/*.csv), shared by every screen. Change <see cref="Data"/>, then call <see cref="Save"/>.
    /// </summary>
    public static class Club
    {
        private static MasterData _master;
        private static ClubSave _data;
        private static SaveData _loadedFrom;

        public static MasterData Master => _master ?? (_master = LoadMaster());

        public static ClubSave Data
        {
            get
            {
                var save = Services.Get<SaveService>().Data;
                if (_data == null || !ReferenceEquals(_loadedFrom, save))
                {
                    _data = save.GetGame<ClubSave>(); // a reloaded or reset save gets read again
                    _loadedFrom = save;
                    _data.StartIfNew(Master);
                }

                return _data;
            }
        }

        public static long Now => DateTime.UtcNow.Ticks;

        /// <summary>Today on the player's calendar, as days since 1970-01-01 (the daily gift resets at local midnight).</summary>
        public static int Today => (int)(DateTime.Now.Date - new DateTime(1970, 1, 1)).TotalDays;

        /// <summary>Boosters bought on the level start popup, placed on the board by the level that starts next.</summary>
        public static readonly System.Collections.Generic.List<string> PendingBoosters = new System.Collections.Generic.List<string>();

        /// <summary>Riders granted outside Recruit (the free rider after a level) that Home still has to reveal.</summary>
        public static readonly System.Collections.Generic.List<PullOutcome> PendingReveals = new System.Collections.Generic.List<PullOutcome>();

        /// <summary>First-session script: a new player boots straight into the next level, skipping Home.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OpenNewPlayersInALevel() =>
            GameBootstrap.FirstState = () => Data.InFirstSession(Master) ? AppState.Game : AppState.Title;

        public static void Save()
        {
            var service = Services.Get<SaveService>();
            service.Data.SetGame(Data);
            service.Save();
        }

        private static MasterData LoadMaster()
        {
            var md = MasterData.Parse(table =>
            {
                var file = Resources.Load<TextAsset>($"MasterData/{table}");
                return file != null ? file.text : null;
            });
            var problems = md.Validate();
            if (problems.Count > 0)
            {
                Debug.LogError("[MasterData] " + string.Join("\n", problems));
            }

            return md;
        }
    }
}
