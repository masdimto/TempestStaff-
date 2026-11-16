using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace TempestStaffBuffPlugin
{
    [ApiVersion(2, 1)]
    public class TempestStaffBuff : TerrariaPlugin
    {
        public override string Name => "TempestStaffBuff";
        public override System.Version Version => new System.Version(1, 0, 0);
        public override string Author => "CustomPlugin";
        public override string Description => "Buff Tempest Staff: Proyektil Mini Sharkron otomatis homing ke musuh terdekat (0.18).";

        private const float HomingStrength = 0.18f;
        private const float MaxHomingRange = 900f;

        public TempestStaffBuff(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            Commands.ChatCommands.Add(new Command("tempest.buff", CheckStatusCommand, "tempest", "tempestbuff")
            {
                HelpText = "Mengecek status buff Tempest Staff."
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
            base.Dispose(disposing);
        }

        private void CheckStatusCommand(CommandArgs args)
        {
            args.Player.SendSuccessMessage("[TempestStaffBuff] Aktif! Mini Sharkron otomatis mengejar musuh terdekat.");
        }

        private void OnGameUpdate(EventArgs args)
        {
            for (int p = 0; p < Main.maxProjectiles; p++)
            {
                Projectile proj = Main.projectile[p];

                if (!proj.active || proj.type != ProjectileID.MiniSharkron)
                    continue;

                int owner = proj.owner;
                if (owner < 0 || owner >= Main.maxPlayers)
                    continue;

                TSPlayer tsPlayer = TShock.Players[owner];
                if (tsPlayer == null || !tsPlayer.Active || tsPlayer.TPlayer == null)
                    continue;

                if (!tsPlayer.HasPermission("tempest.buff"))
                    continue;

                NPC target = FindClosestTarget(proj.Center, MaxHomingRange);
                if (target != null)
                {
                    float speed = proj.velocity.Length();
                    if (speed < 12f)
                        speed = 16f;

                    Vector2 desiredDir = target.Center - proj.Center;
                    if (desiredDir.LengthSquared() > 0.01f)
                    {
                        desiredDir.Normalize();
                        Vector2 targetVel = desiredDir * speed;
                        proj.velocity = Vector2.Lerp(proj.velocity, targetVel, HomingStrength);

                        if (proj.timeLeft % 3 == 0)
                        {
                            NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, -1, null, p);
                        }
                    }
                }
            }
        }

        private NPC FindClosestTarget(Vector2 position, float maxRange)
        {
            NPC closest = null;
            float bestDistSq = maxRange * maxRange;

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal || npc.lifeMax <= 5)
                    continue;

                float distSq = Vector2.DistanceSquared(position, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closest = npc;
                }
            }

            return closest;
        }
    }
}
