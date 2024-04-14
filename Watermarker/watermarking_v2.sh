#!/usr/bin/env bash

folder=./to_watermark
destFolder=$1

listePhotos=$(ls $folder/processing/*.*)
echo $folder $destFolder $listePhotos
./watermark watermark_solo.png watermark_solo_mask.png $listePhotos
mv $folder/processing/w_* $folder/processed/
mv $folder/processing/* $destFolder/raw
mv $folder/processed/* $destFolder