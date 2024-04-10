#include <opencv2/objdetect.hpp>
#include <opencv2/highgui.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/core.hpp>
#include <opencv2/video.hpp>
#include <opencv2/videoio.hpp>

#include <iostream>
#include <stdio.h>
#include <string>

#include "Watermark.h"

using namespace cv;
using namespace std;

char* clone(char* src)
{
	char* dest = (char*)calloc(255, sizeof(char));
	int i = 0;

	while (src[i] != 0)
	{
		dest[i] = src[i];
		i++;
	}
	dest[i] = 0;
	return dest;
}

Mat getImage(const char* path)
{
	Mat img = imread(path, IMREAD_ANYCOLOR);
	if (img.empty())
	{
		cout << "Erreur de lecture" << endl;
		exit(-1);
	}
	return img;
}

string wing(string path)
{
	int last = path.find_last_of('/');
	string pathBegin = path.substr(0, last + 1);
	string fileName = "w_";
	fileName.append(path.substr(last + 1));
	pathBegin.append(fileName);

	return pathBegin;
}

int ProcessImages(int argc, char** argv)
{
	if (argc < 4)
	{
		cout << "Usage: " << argv[0] << " <fichier_watermark> <masque_watermark> <photo1> [photo2] ..." << endl;
		exit(EXIT_FAILURE);
	}

	const char* watermarkFile = argv[1];
	const char* maskFile = argv[2];
	char* prefix = (char*)calloc(255, sizeof(char));
	prefix[0] = 'w';
	prefix[1] = '_';

	vector<char*> images;
	int posX, posY;
	int i;
	double facteur, facteur2;


	Mat traitement;
	Mat watermarked;
	Mat extract;
	Mat watermark = getImage(watermarkFile);
	watermark.convertTo(watermark, CV_32FC3);
	Mat mask = getImage(maskFile);
	mask.convertTo(mask, CV_32FC3, 1.0 / 255);

	Mat sizedWatermark;
	Mat sizedMask;

	for (i = 3; i < argc; i++)
	{
		images.push_back(argv[i]);
	}

	for (i = 0; (unsigned long)i < images.size(); i++)
	{
		traitement = getImage(images[i]);
		traitement.convertTo(traitement, CV_32FC3);

		facteur = (double)max(traitement.cols, traitement.rows);
		facteur2 = (double)max(watermark.cols, watermark.rows);

		posX = traitement.cols / 20;
		posY = traitement.rows - 3 * facteur / 20;
		cv::resize(watermark, sizedWatermark, cv::Size(), facteur / 10.0 / facteur2, facteur / 10.0 / facteur2);
		cv::resize(mask, sizedMask, cv::Size(), facteur / 10.0 / facteur2, facteur / 10.0 / facteur2);

		watermarked = traitement.clone();
		extract = watermarked(cv::Rect(posX, posY, sizedWatermark.cols, sizedWatermark.rows));

		multiply(sizedMask, sizedWatermark, sizedWatermark);
		multiply(Scalar::all(1.0) - sizedMask, extract, extract);
		add(sizedWatermark, extract, extract);

		extract.copyTo(watermarked(cv::Rect(posX, posY, sizedWatermark.cols, sizedWatermark.rows)));

		imwrite(wing(images[i]), watermarked);
	}

	return 0;
}