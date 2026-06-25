using _Project.Gameplay.TaskSystem;
using UnityEngine;

public class BuildGhostController : MonoBehaviour
{
    private const float GHOST_ALPHA = 0.5f;

    private GameObject _currentGhost;
    private SpriteRenderer _sprite;
    private int _rotationIndex;

    public int RotationIndex
    {
        get
        {
            return _rotationIndex;
        }
    }

    public GameObject CurrentGhost
    {
        get
        {
            return _currentGhost;
        }
    }

    public void CreateGhost(GameObject prefab, Vector3 position, BuildingData data, int rotationIndex)
    {
        ClearGhost();
        _rotationIndex = data != null ? data.GetRotationIndex(data.GetRotationAngle(rotationIndex)) : 0;
        Quaternion rotation = data != null ? data.GetWorldRotation(_rotationIndex) : Quaternion.identity;

        _currentGhost = Instantiate(prefab, position, rotation);
        _sprite = _currentGhost.GetComponentInChildren<SpriteRenderer>();
        DisableRuntimeComponents();

        if (data != null)
        {
            data.ApplyRotationSprite(_currentGhost, _rotationIndex);
        }

        SetTransparent();
    }


    public void MoveGhost(Vector3 position)
    {
        if (_currentGhost == null)
        {
            return;
        }

        _currentGhost.transform.position = position;
    }

    public void RotateGhost(BuildingData data)
    {
        if (_currentGhost == null)
        {
            return;
        }

        if (data == null)
        {
            return;
        }

        _rotationIndex++;
        _currentGhost.transform.rotation = data.GetWorldRotation(_rotationIndex);

        if (_sprite != null)
        {
            data.ApplyRotationSprite(_currentGhost, _rotationIndex);
        }
    }

    public Quaternion GetRotation()
    {
        if (_currentGhost == null)
        {
            return Quaternion.identity;
        }

        return _currentGhost.transform.rotation;
    }

    public void SetColor(bool canPlace)
    {
        if (_currentGhost == null)
        {
            return;
        }


        if (_sprite == null)
        {
            return;
        }

        if (canPlace)
        {
            _sprite.color = new Color(0f, 1f, 0f, GHOST_ALPHA);
        }
        else
        {
            _sprite.color = new Color(1f, 0f, 0f, GHOST_ALPHA);
        }
    }

    public void ClearGhost()
    {
        if (_currentGhost != null)
        {
            Destroy(_currentGhost);
            _currentGhost = null;
            _sprite = null;
        }
    }

    private void SetTransparent()
    {
        if (_currentGhost == null)
        {
            return;
        }

        if (_sprite == null)
        {
            return;
        }

        Color color = _sprite.color;
        color.a = GHOST_ALPHA;
        _sprite.color = color;
    }

    private void DisableRuntimeComponents()
    {
        if (_currentGhost == null)
        {
            return;
        }

        InteractTaskObject[] interactObjects = _currentGhost.GetComponentsInChildren<InteractTaskObject>(true);
        for (int i = 0; i < interactObjects.Length; i++)
        {
            interactObjects[i].enabled = false;
        }

        Collider2D[] colliders = _currentGhost.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }
}
