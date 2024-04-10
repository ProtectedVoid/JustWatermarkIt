#!/usr/bin/env bash

folder=../mes_sessions

for i in $(ls $folder)
do
    listePhotos=$(ls $folder/$i/raw/*.*)
    ./watermark watermark_solo.png watermark_solo_mask.png $listePhotos
    mv $folder/$i/raw/w_* $folder/$i
done