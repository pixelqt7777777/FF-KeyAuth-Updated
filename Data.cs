using System.Numerics;

namespace CSharp_ImGui_Client
{
    internal static class Data
    {
        internal static void Work()
        {
            while (Core.Running)
            {
                Core.HaveMatrix = false;

                var rBaseGameFacade = SocketMemory.Read<uint>(Offsets.Il2Cpp + Offsets.InitBase, out var baseGameFacade);
                if (!rBaseGameFacade || baseGameFacade == 0) { ResetCache(); continue; }

                var rGameFacade = SocketMemory.Read<uint>(baseGameFacade, out var gameFacade);
                if (!rGameFacade || gameFacade == 0) { ResetCache(); continue; }

                var rStaticGameFacade = SocketMemory.Read<uint>(gameFacade + Offsets.StaticClass, out var staticGameFacade);
                if (!rStaticGameFacade || staticGameFacade == 0) { ResetCache(); continue; }

                var rCurrentGame = SocketMemory.Read<uint>(staticGameFacade, out var currentGame);
                if (!rCurrentGame || currentGame == 0) { ResetCache(); continue; }

                var rCurrentMatch = SocketMemory.Read<uint>(currentGame + Offsets.CurrentMatch, out var currentMatch);
                if (!rCurrentMatch || currentMatch == 0) { ResetCache(); continue; }

                var rLocalPlayer = SocketMemory.Read<uint>(currentMatch + Offsets.LocalPlayer, out var localPlayer);
                if (!rLocalPlayer || localPlayer == 0) continue;

                Core.LocalPlayer = localPlayer;

                var rMainTransform = SocketMemory.Read<uint>(localPlayer + Offsets.MainCameraTransform, out var mainTransform);
                if (!rMainTransform || mainTransform == 0) continue;

                var rMainTransformPos = Transform.GetPosition(mainTransform, out var mainPos);
                if (rMainTransformPos) Core.LocalMainCamera = mainPos;

                var rFollowCamera = SocketMemory.Read<uint>(localPlayer + Offsets.FollowCamera, out var followCamera);
                if (!rFollowCamera || followCamera == 0) continue;

                var rCamera = SocketMemory.Read<uint>(followCamera + Offsets.Camera, out var camera);
                if (!rCamera || camera == 0) continue;

                var rCameraBase = SocketMemory.Read<uint>(camera + 0x8, out var cameraBase);
                if (!rCameraBase || cameraBase == 0) continue;

                Core.HaveMatrix = true;

                var rViewMatrix = SocketMemory.Read<Matrix4x4>(cameraBase + Offsets.ViewMatrix, out var viewMatrix);
                if (!rViewMatrix) continue;
                Core.CameraMatrix = viewMatrix;

                foreach (var entity in GetEntities(currentGame, Offsets.DictionaryEntities))
                {
                    if (entity == 0 || entity == localPlayer) continue;

                    if (Core.Entities.TryGetValue(entity, out var player))
                    {
                        player.Address = entity;

                        if (player.IsTeam == Bool3.True) continue;

                        if (player.IsTeam == Bool3.Unknown)
                        {
                            var rAvatarManager = SocketMemory.Read<uint>(entity + Offsets.AvatarManager, out var avatarManager);
                            if (rAvatarManager && avatarManager != 0)
                            {
                                var rAvatar = SocketMemory.Read<uint>(avatarManager + Offsets.Avatar, out var avatar);
                                if (rAvatar && avatar != 0)
                                {
                                    var rIsVisible = SocketMemory.Read<bool>(avatar + Offsets.Avatar_IsVisible, out var isVisible);
                                    if (rIsVisible && isVisible)
                                    {
                                        var rAvatarData = SocketMemory.Read<uint>(avatar + Offsets.Avatar_Data, out var avatarData);
                                        if (rAvatarData && avatarData != 0)
                                        {
                                            var rIsTeam = SocketMemory.Read<bool>(avatarData + Offsets.Avatar_Data_IsTeam, out var isTeam);
                                            if (rIsTeam)
                                            {
                                                if (isTeam)
                                                    player.IsTeam = Bool3.True;
                                                else
                                                {
                                                    player.IsTeam = Bool3.False;
                                                    player.IsKnown = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (!player.IsKnown) continue;

                        var rIsDead = SocketMemory.Read<bool>(entity + Offsets.Player_IsDead, out var isDead);
                        if (rIsDead) player.IsDead = isDead;

                        if (SocketMemory.Read<uint>(entity + Offsets.Player_ShadowBase, out var shadowBase) && shadowBase != 0)
                        {
                            if (SocketMemory.Read<int>(shadowBase + Offsets.XPose, out var xpose))
                                player.IsKnocked = xpose == 8;
                        }

                        var rNameAddr = SocketMemory.Read<uint>(entity + Offsets.Player_Name, out var nameAddr);
                        if (rNameAddr && nameAddr != 0)
                        {
                            var rNameLen = SocketMemory.Read<int>(nameAddr + 0x8, out var nameLen);
                            if (rNameLen && nameLen > 0)
                            {
                                int charCount = Math.Min(nameLen, 512);
                                var name = SocketMemory.ReadString(nameAddr + 0xC, charCount * 2)?.Trim('\0');
                                if (!string.IsNullOrEmpty(name))
                                {
                                    player.Name = name;
                                }
                            }
                        }

                        if (ExtEspConfig.ESPHealth)
                        {
                            var rDataPool = SocketMemory.Read<uint>(entity + Offsets.Player_Data, out var dataPool);
                            if (rDataPool && dataPool != 0)
                            {
                                var rPoolObj = SocketMemory.Read<uint>(dataPool + 0x8, out var poolObj);
                                if (rPoolObj && poolObj != 0)
                                {
                                    var rPool = SocketMemory.Read<uint>(poolObj + 0x10, out var pool);
                                    if (rPool && pool != 0)
                                    {
                                        var rHealth = SocketMemory.Read<short>(pool + 0x10, out var health);
                                        if (rHealth && health != 0) player.Health = health;
                                    }
                                }
                            }
                        }

                        var boneOffsets = new[]
                        {
                            Bones.Head, Bones.LeftWrist, Bones.Spine, Bones.Hip, Bones.Root,
                            Bones.RightCalf, Bones.LeftCalf, Bones.RightFoot, Bones.LeftFoot,
                            Bones.RightWrist, Bones.LeftHand, Bones.LeftShoulder, Bones.RightShoulder,
                            Bones.RightWristJoint, Bones.LeftWristJoint, Bones.LeftElbow, Bones.RightElbow
                        };

                        foreach (var offset in boneOffsets)
                        {
                            var rBone = SocketMemory.Read<uint>(entity + (uint)offset, out var bone);
                            if (rBone && bone != 0)
                            {
                                var rBonePos = Transform.GetNodePosition(bone, out var boneTransform);
                                if (rBonePos)
                                {
                                    switch (offset)
                                    {
                                        case Bones.Head: player.Head = boneTransform; break;
                                        case Bones.LeftWrist: player.LeftWrist = boneTransform; break;
                                        case Bones.Spine: player.Spine = boneTransform; break;
                                        case Bones.Hip: player.Hip = boneTransform; break;
                                        case Bones.Root: player.Root = boneTransform; break;
                                        case Bones.RightCalf: player.RightCalf = boneTransform; break;
                                        case Bones.LeftCalf: player.LeftCalf = boneTransform; break;
                                        case Bones.RightFoot: player.RightFoot = boneTransform; break;
                                        case Bones.LeftFoot: player.LeftFoot = boneTransform; break;
                                        case Bones.RightWrist: player.RightWrist = boneTransform; break;
                                        case Bones.LeftHand: player.LeftHand = boneTransform; break;
                                        case Bones.LeftShoulder: player.LeftShoulder = boneTransform; break;
                                        case Bones.RightShoulder: player.RightShoulder = boneTransform; break;
                                        case Bones.RightWristJoint: player.RightWristJoint = boneTransform; break;
                                        case Bones.LeftWristJoint: player.LeftWristJoint = boneTransform; break;
                                        case Bones.RightElbow: player.RightElbow = boneTransform; break;
                                        case Bones.LeftElbow: player.LeftElbow = boneTransform; break;
                                    }
                                    player.Distance = Vector3.Distance(Core.LocalMainCamera, player.Head);
                                }
                            }
                        }
                    }
                    else
                    {
                        Core.Entities[entity] = new Entity
                        {
                            IsTeam = Bool3.Unknown,
                            IsKnown = false,
                            IsDead = false,
                            Health = 0,
                            IsKnocked = false,
                            Head = Vector3.Zero,
                            LeftWrist = Vector3.Zero,
                            Spine = Vector3.Zero,
                            Root = Vector3.Zero,
                            Hip = Vector3.Zero,
                            RightCalf = Vector3.Zero,
                            LeftCalf = Vector3.Zero,
                            RightFoot = Vector3.Zero,
                            LeftFoot = Vector3.Zero,
                            RightWrist = Vector3.Zero,
                            LeftHand = Vector3.Zero,
                            RightShoulder = Vector3.Zero,
                            RightWristJoint = Vector3.Zero,
                            LeftWristJoint = Vector3.Zero,
                            RightElbow = Vector3.Zero,
                            LeftElbow = Vector3.Zero,
                            Name = ""
                        };
                    }
                }
            }

            static List<uint> GetEntities(uint baseGame, uint offset)
            {
                List<uint> entityList = new();

                if (!SocketMemory.Read<uint>(baseGame + offset, out uint dict) || dict == 0)
                    return entityList;

                if (!SocketMemory.Read<int>(dict + 0x10, out int count) || count < 1 || count > 10000)
                    return entityList;

                if (!SocketMemory.Read<uint>(dict + 0xC, out uint entries) || entries == 0)
                    return entityList;

                uint start = entries + 0x10;

                for (uint i = 0; i < count; i++)
                {
                    uint entry = start + (i * 0x10);

                    if (!SocketMemory.Read<int>(entry + 0x0, out int hash) || hash < 0)
                        continue;

                    if (!SocketMemory.Read<uint>(entry + 0xC, out uint entity) || entity == 0)
                        continue;

                    entityList.Add(entity);
                }

                return entityList;
            }

            static void ResetCache()
            {
                Core.Entities = new();
            }
        }
    }
}
