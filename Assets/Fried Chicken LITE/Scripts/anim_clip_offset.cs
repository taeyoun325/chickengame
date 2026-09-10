using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace namespace_animclip_offset
{
    public class anim_clip_offset : MonoBehaviour
    {
        private Animator animator;

        void Start()
        {
            animator = GetComponentInChildren<Animator>(); // supports child Animator
            StartCoroutine(Play_Animationclip_offset());
        }

        IEnumerator Play_Animationclip_offset()
        {
            yield return null; // wait 1 frame (Animator ready)

            AnimatorClipInfo[] clip_name = animator.GetCurrentAnimatorClipInfo(0);

            if (clip_name.Length == 0) yield break;

            AnimationClip anim_clip = clip_name[0].clip;

            float time = Random.Range(0f, anim_clip.length);
            int clip_position = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            animator.Play(clip_position, 0, time / anim_clip.length);
        }
    }
}