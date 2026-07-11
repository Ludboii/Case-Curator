using UnityEngine;

public class CaseInspectCompletionPopupUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        Close();
    }

    public void Open(CaseData caseData)
    {
        if (root != null)
            root.SetActive(true);
    }

    public void Close()
    {
        if (root != null)
            root.SetActive(false);
    }
}
