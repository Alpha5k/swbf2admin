#pragma once
#include <stdarg.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>

#include <iostream>
#include <fstream>
#include <mutex>

using std::string;
using std::ofstream;
using std::mutex;
using std::unique_lock;

enum LogLevel {
	LogLevel_VERBOSE = 0,
	LogLevel_INFO = 1,
	LogLevel_WARNING = 2,
	LogLevel_ERROR = 3
};

static const char* LOG_LEVELS[] =
{
	"DEBUG | ",
	"INFO  | ",
	"WARN  | ",
	"ERROR | "
};

class _Logger
{
public:
	_Logger();
	~_Logger();
	void log(LogLevel level, const char* msg, ...);
	void setMinLevelStdout(LogLevel level);
	void setMinLevelFile(LogLevel level);
	void SetFileName(string const &fileName);

private:
	void logToFile(const string &s);
	LogLevel minLevelStdout = LogLevel_ERROR;
	LogLevel minLevelFile = LogLevel_ERROR;
	mutex mtx;
	string logFile = "./log.txt";
};

static _Logger Logger;