using UnityEngine;

/* RealChuteLite is the work of Christophe Savard (stupid_chris), and is licensed the same way than the rest of FAR is.
 * If you have any questions about this code, or want to report something, don't bug ferram about it, ask me
 * directly on GitHub, the forums, or IRC. */

namespace FerramAerospaceResearch.RealChuteLite
{
    public static class PartExtensions
    {
        #region Methods
        /// <summary>
        /// Returns the total mass of the part
        /// </summary>
        public static float TotalMass(this Part part)
        {
            return part.physicalSignificance != Part.PhysicalSignificance.NONE ? part.mass + part.GetResourceMass() : 0;
        }

        /// <summary>
        /// Initiates an animation for later use
        /// </summary>
        /// <param name="animationName">Name of the animation</param>
        public static void InitiateAnimation(this Part part, string animationName)
        {
            foreach (Animation animation in part.FindModelAnimators(animationName))
            {
                AnimationState state = animation[animationName];
                state.normalizedTime = 0;
                state.normalizedSpeed = 0;
                state.enabled = false;
                state.wrapMode = WrapMode.Clamp;
                state.layer = 1;
            }
        }

        /// <summary>
        /// Plays an animation at a given speed
        /// </summary>
        /// <param name="animationName">Name of the animation</param>
        /// <param name="animationSpeed">Speed to play the animation at</param>
        public static void PlayAnimation(this Part part, string animationName, float animationSpeed)
        {
            foreach (Animation animation in part.FindModelAnimators(animationName))
            {
                AnimationState state = animation[animationName];
                state.normalizedTime = 0;
                state.normalizedSpeed = animationSpeed;
                state.enabled = true;
                animation.Play(animationName);
            }
        }

        /// <summary>
        /// Skips directly to the given time of the animation
        /// </summary>
        /// <param name="animationName">Name of the animation to skip to</param>
        /// <param name="animationSpeed">Speed of the animation after the skip</param>
        /// <param name="animationTime">Normalized time skip</param>
        public static void SkipToAnimationTime(this Part part, string animationName, float animationSpeed, float animationTime)
        {
            foreach (Animation animation in part.FindModelAnimators(animationName))
            {
                AnimationState state = animation[animationName];
                state.normalizedTime = animationTime;
                state.normalizedSpeed = animationSpeed;
                state.enabled = true;
                animation.Play(animationName);
            }
        }
        #endregion
    }
}
