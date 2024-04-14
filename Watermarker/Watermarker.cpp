#include <opencv2/objdetect.hpp>
#include <opencv2/highgui.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/core.hpp>
#include <opencv2/video.hpp>
#include <opencv2/videoio.hpp>

#include <iostream>
#include <stdio.h>
#include <string>

#include "Watermarker.hpp"

using namespace cv;
using namespace std;

const string Watermarker::TO_WATERMARK_FOLDER = ".\\to_watermark";

void Watermarker::printFeedback(int current, int total)
{
	system("cls");
	cout << "=== PICTURES PROCESSING ===" << endl;
	cout << current << " files on " << total << " processed" << endl;
}

/// <summary>
/// Permet de lire une image
/// </summary>
/// <param name="path">Le chemin du fichier</param>
/// <returns>L'image lue</returns>
Mat Watermarker::getImage(string path)
{
	Mat img = imread(path, IMREAD_ANYCOLOR);
	if (img.empty())
	{
		cout << "Reading file error : " << path << endl;
		exit(-1);
	}
	return img;
}

/// <summary>
/// Rajoute "w_" devant le nom d'image. Permet de différencier les images originales des watermarkées
/// </summary>
/// <param name="path">Le nom de l'image</param>
/// <returns>Le nouveau nom</returns>
string Watermarker::wing(string path)
{
	size_t last = path.find_last_of('\\');
	string pathBegin = path.substr(0, last + 1);
	string fileName = "w_";
	fileName.append(path.substr(last + 1));
	pathBegin.append(fileName);

	return pathBegin;
}
/// <summary>
/// Lit les images passées en paramètres et les watermark
/// </summary>
/// <param name="argc">Le nombre d'arguments</param>
/// <param name="argv"></param>
/// <returns></returns>
int Watermarker::ProcessImages(char* watermark_file, char* watermark_mask, vector<string> pictures)
{
	char* prefix = (char*)calloc(255, sizeof(char));
	prefix[0] = 'w';
	prefix[1] = '_';

	int posX, posY;
	int i;
	double facteur, facteur2;
	size_t pictureCount = pictures.size();


	Mat traitement;
	Mat watermarked;
	Mat extract;
	Mat watermark = getImage(watermark_file);
	watermark.convertTo(watermark, CV_32FC3);
	Mat mask = getImage(watermark_mask);
	mask.convertTo(mask, CV_32FC3, 1.0 / 255);

	Mat sizedWatermark;
	Mat sizedMask;

	printFeedback(0, pictureCount);

	for (i = 0; i < pictureCount; i++)
	{
		traitement = getImage(pictures[i]);
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

		imwrite(wing(pictures[i]), watermarked);

		printFeedback(i + 1, pictureCount);
	}

	return 0;
}