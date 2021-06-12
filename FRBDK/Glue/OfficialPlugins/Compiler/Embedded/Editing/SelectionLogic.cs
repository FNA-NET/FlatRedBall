﻿using FlatRedBall;
using FlatRedBall.Graphics;
using FlatRedBall.Gui;
using FlatRedBall.Math.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace {ProjectNamespace}.GlueControl.Editing
{
    public static class SelectionLogic
    {
        static List<PositionedObject> tempPunchThroughList = new List<PositionedObject>();

        public static PositionedObject GetEntityOver(PositionedObject currentEntity, SelectionMarker selectionMarker,
            bool punchThrough, ElementEditingMode elementEditingMode)
        {
            PositionedObject entityOver = null;
            if(currentEntity != null && punchThrough == false)
            {
                if(IsCursorOver(currentEntity) || selectionMarker.IsCursorOverThis())
                {
                    entityOver = currentEntity;
                }
            }

            if(punchThrough)
            {
                tempPunchThroughList.Clear();
            }

            if(entityOver == null)
            {
                IList<PositionedObject> list = null;

                if(elementEditingMode == ElementEditingMode.EditingScreen)
                {
                    list = SpriteManager.ManagedPositionedObjects;
                }
                else if(elementEditingMode == ElementEditingMode.EditingEntity)
                {
                    if(SpriteManager.ManagedPositionedObjects.Count > 0)
                    {
                        list = SpriteManager.ManagedPositionedObjects[0].Children;
                    }
                }

                if(list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var objectAtI = list[i] as PositionedObject;

                        if (IsSelectable(objectAtI) && IsCursorOver(objectAtI))
                        {
                            if (punchThrough)
                            {
                                tempPunchThroughList.Add(objectAtI);
                            }
                            else
                            {
                                entityOver = objectAtI;
                                break;
                            }
                        }
                    }
                }
            }

            if(punchThrough)
            {
                if(tempPunchThroughList.Count == 0)
                {
                    entityOver = null;
                }
                else if(tempPunchThroughList.Count == 1)
                {
                    entityOver = tempPunchThroughList[0];
                }
                else if(tempPunchThroughList.Contains(currentEntity) == false)
                {
                    // just pick the first
                    entityOver = tempPunchThroughList[0];
                }
                else
                {
                    var index = tempPunchThroughList.IndexOf(currentEntity);
                    if(index < tempPunchThroughList.Count - 1)
                    {
                        entityOver = tempPunchThroughList[index + 1];
                    }
                    else
                    {
                        entityOver = tempPunchThroughList[0];
                    }
                }
            }

            return entityOver;
        }

        private static bool IsSelectable(PositionedObject objectAtI)
        {
            return objectAtI.CreationSource == "Glue";
        }

        private static bool IsCursorOver(PositionedObject objectAtI)
        {
            var cursor = GuiManager.Cursor;
            var worldX = cursor.WorldX;
            var worldY = cursor.WorldY;

            GetDimensionsFor(objectAtI, out float minX, out float maxX, out float minY, out float maxY);

            return worldX >= minX &&
                    worldX <= maxX &&
                    worldY >= minY &&
                    worldY <= maxY;
        }

        internal static void GetDimensionsFor(PositionedObject itemOver,
            out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = itemOver.X;
            maxX = itemOver.X;
            minY = itemOver.Y;
            maxY = itemOver.Y;
            GetDimensionsForInner(itemOver, ref minX, ref maxX, ref minY, ref maxY);

            const float minDimension = 16;
            if(maxX - minX < minDimension)
            {
                var extraToAdd = minDimension - (maxX - minX);

                minX -= extraToAdd / 2.0f;
                maxX += extraToAdd / 2.0f;
            }

            if(maxY - minY < minDimension)
            {
                var extraToAdd = minDimension - (maxY - minY);

                minY -= extraToAdd / 2.0f;
                maxY += extraToAdd / 2.0f;
            }
        }

        private static void GetDimensionsForInner(PositionedObject itemOver,
            ref float minX, ref float maxX, ref float minY, ref float maxY)
        {
            if (itemOver is IScalable asScalable)
            {
                minX = Math.Min(minX, itemOver.X - asScalable.ScaleX);
                maxX = Math.Max(maxX, itemOver.X + asScalable.ScaleX);

                minY = Math.Min(minY, itemOver.Y - asScalable.ScaleY);
                maxY = Math.Max(maxY, itemOver.Y + asScalable.ScaleY);
            }
            else
            {
                for (int i = 0; i < itemOver.Children.Count; i++)
                {
                    var child = itemOver.Children[i];

                    GetDimensionsForInner(child, ref minX, ref maxX, ref minY, ref maxY);
                }
            }
        }
    }
}