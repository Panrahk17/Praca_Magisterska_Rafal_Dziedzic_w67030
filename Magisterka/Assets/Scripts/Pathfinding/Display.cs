using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Display : MonoBehaviour
{
    public static Display Instance;
    
    [SerializeField] Material defaultMaterial;
    Texture2D displayPlaneTexture;
    Texture2D heatPlaneTexture;
    Texture2D chunkPlaneTexture;
    [SerializeField] Renderer planeRenderer;
    [SerializeField] Renderer heatPlaneRenderer;
    [SerializeField] Renderer chunkPlaneRenderer;

    [SerializeField] Transform[] displayPlanes;
    int id = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else Destroy(gameObject);
    }

    public void Initialize(int displayXSize, int displayYSize)
    {
        //display plane
        displayPlaneTexture = new Texture2D(displayXSize, displayYSize);
        planeRenderer.material = defaultMaterial;
        displayPlaneTexture.filterMode = FilterMode.Point;
        planeRenderer.material.mainTexture = displayPlaneTexture;

        //heatmap plane
        heatPlaneTexture = new Texture2D(displayXSize, displayYSize);
        heatPlaneRenderer.material = defaultMaterial;
        heatPlaneTexture.filterMode = FilterMode.Point;
        heatPlaneRenderer.material.mainTexture = heatPlaneTexture;

        //chunk plane
        chunkPlaneTexture = new Texture2D(displayXSize, displayYSize);
        chunkPlaneRenderer.material = defaultMaterial;
        chunkPlaneTexture.filterMode = FilterMode.Point;
        chunkPlaneRenderer.material.mainTexture = chunkPlaneTexture;
    }
    
    void Update()
    {
        if (displayPlanes.Length > 1)
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                displayPlanes[id].transform.position += new Vector3(0, 0, 1);
                id--;
                if (id < 0)
                {
                    id = displayPlanes.Length - 1;
                }
                displayPlanes[id].transform.position += new Vector3(0, 0, -1);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                displayPlanes[id].transform.position += new Vector3(0, 0, 1);
                id++;
                id %= displayPlanes.Length;
                displayPlanes[id].transform.position += new Vector3(0, 0, -1);
            }

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                for (int i = 0; i < displayPlaneTexture.Size().x; i++)
                {
                    for (int j = 0; j < displayPlaneTexture.Size().y; j++)
                    {
                        Color heatColor = heatPlaneTexture.GetPixel(i, j);
                        if (heatColor != Color.black)
                        {
                            Color newColor = displayPlaneTexture.GetPixel(i, j) + heatColor;
                            displayPlaneTexture.SetPixel(i, j, newColor);
                        }
                    }
                }
                ApplyDisplayChanges(DisplayType.Main);
            }
        }
    }
    public Vector3 GetDisplayWorldPosition()
    {
        return planeRenderer.gameObject.transform.position;
    }
    public Vector3 GetDisplaySize()
    {
        return planeRenderer.GetComponent<MeshFilter>().mesh.bounds.size;
    }

    public void SetPixel(int tileX, int tileY, Color newColor, DisplayType display)
    {
        Texture2D targetTexture = null;
        if (display == DisplayType.Main)
        {
            targetTexture = displayPlaneTexture;
        }
        else if (display == DisplayType.HeatMap)
        {
            targetTexture = heatPlaneTexture;
        }
        else if (display == DisplayType.Chunks)
        {
            targetTexture = chunkPlaneTexture;
        }

        if (targetTexture != null)
        {
            targetTexture.SetPixel(tileX, tileY, newColor);
        }
    }
    public Color GetPixel(int tileX, int tileY, DisplayType display)
    {
        Texture2D targetTexture = null;
        if (display == DisplayType.Main)
        {
            targetTexture = displayPlaneTexture;
        }
        else if (display == DisplayType.HeatMap)
        {
            targetTexture = heatPlaneTexture;
        }
        else if (display == DisplayType.Chunks)
        {
            targetTexture = chunkPlaneTexture;
        }

        if (targetTexture != null)
        {
            return targetTexture.GetPixel(tileX, tileY);
        }
        else return new Color();
    }
    public void ApplyDisplayChanges(DisplayType display)
    {
        Texture2D textureToUpdate = null;
        if (display == DisplayType.Main)
        {
            textureToUpdate = displayPlaneTexture;
        }
        else if (display == DisplayType.HeatMap)
        {
            textureToUpdate = heatPlaneTexture;
        }
        else if (display == DisplayType.Chunks)
        {
            textureToUpdate = chunkPlaneTexture;
        }

        if (textureToUpdate != null)
        {            
            textureToUpdate.Apply();
        }        
    }    
}
public enum DisplayType
{
    Main,
    HeatMap,
    Chunks
}
