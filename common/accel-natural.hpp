#pragma once

#include <math.h>

#include "accel-base.hpp"

namespace rawaccel {

	/// <summary> Struct to hold "natural" (vanishing difference) acceleration implementation. </summary>
	struct accel_natural : accel_base {
		double limit = 1;
		double midpoint = 0;

		accel_natural(const accel_args& args) : accel_base(args) {
			verify(args);

			limit = args.limit - 1;
			speed_coeff /= limit;
		}

		inline double accelerate(double speed) const {
			// f(x) = k(1-e^(-mx))
			return limit - (limit * exp(-speed_coeff * speed));
		}

		void verify(const accel_args& args) const {
			if (args.limit <= 1) bad_arg("limit must be greater than 1");
		}
	};

}