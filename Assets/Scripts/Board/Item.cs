using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[Serializable]
public class Item
{
    public Cell Cell { get; private set; }

    public Transform View { get; private set; }


    public virtual void SetView()
    {
        string prefabname = GetPrefabName();

        if (!string.IsNullOrEmpty(prefabname))
        {
            GameObject prefab = Resources.Load<GameObject>(prefabname);
            if (prefab)
            {
                View = GameObject.Instantiate(prefab).transform;
            }
        }
    }

    protected virtual string GetPrefabName() { return string.Empty; }

    public virtual void SetCell(Cell cell)
    {
        Cell = cell;
    }

    internal void AnimationMoveToPosition()
    {
        if (View == null) return;

        View.DOMove(Cell.transform.position, 0.2f);
    }

    public void SetViewPosition(Vector3 pos)
    {
        if (View)
        {
            View.position = pos;
        }
    }

    public void SetViewRoot(Transform root)
    {
        if (View)
        {
            View.SetParent(root);
        }
    }

    public void SetSortingLayerHigher()
    {
        if (View == null) return;

        SpriteRenderer sp = View.GetComponent<SpriteRenderer>();
        if (sp)
        {
            sp.sortingOrder = 1;
        }
    }


    public void SetSortingLayerLower()
    {
        if (View == null) return;

        SpriteRenderer sp = View.GetComponent<SpriteRenderer>();
        if (sp)
        {
            sp.sortingOrder = 0;
        }

    }

    internal void ShowAppearAnimation()
    {
        if (View == null) return;

        Vector3 scale = View.localScale;
        View.localScale = Vector3.one * 0.1f;
        View.DOScale(scale, 0.1f);
    }

    internal virtual bool IsSameType(Item other)
    {
        return false;
    }

    internal virtual void ExplodeView()
    {
        if (View)
        {
            // Play particle effect nếu có
            PlayExplodeParticle();

            // Trigger animation nếu có Animator
            Animator animator = View.GetComponent<Animator>();
            if (animator != null)
            {
                // Trigger "Explode" animation
                animator.SetTrigger("Explode");

                Debug.Log($"💥 Triggered Explode animation for {View.name}");

                // Đợi animation play xong (giả sử animation dài ~0.3s)
                View.DOScale(0.1f, 0.5f).OnComplete(
                    () =>
                    {
                        GameObject.Destroy(View.gameObject);
                        View = null;
                    }
                );
            }
            else
            {
                // Không có animator -> dùng scale animation như cũ
                View.DOScale(0.1f, 0.1f).OnComplete(
                    () =>
                    {
                        GameObject.Destroy(View.gameObject);
                        View = null;
                    }
                );
            }
        }
    }

    // Play particle effect khi explode
    private void PlayExplodeParticle()
    {
        if (View == null) return;

        // Tìm ParticleSystem trong children
        ParticleSystem particle = View.GetComponentInChildren<ParticleSystem>();
        if (particle != null)
        {
            particle.Play();
            Debug.Log($"✨ Playing particle effect for {View.name}");
        }
    }



    internal void AnimateForHint()
    {
        if (View)
        {
            View.DOPunchScale(View.localScale * 0.1f, 0.1f).SetLoops(-1);
        }
    }

    internal void StopAnimateForHint()
    {
        if (View)
        {
            View.DOKill();
        }
    }

    internal void Clear()
    {
        Cell = null;

        if (View)
        {
            GameObject.Destroy(View.gameObject);
            View = null;
        }
    }
}
