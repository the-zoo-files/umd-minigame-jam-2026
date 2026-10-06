using UnityEngine;

namespace UmdJam.Multiplayer
{
    public sealed class CharacterAnimationEvents : MonoBehaviour
    {
        private CouchPlayerController player;

        public void Configure(CouchPlayerController owner)
        {
            player = owner;
        }

        public void ReleaseFlask()
        {
            player?.ReleaseFlaskFromAnimation();
        }
    }
}
