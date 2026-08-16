import * as React from "react"
import { ChevronDown } from "lucide-react"

import { cn } from "@/lib/utils"

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {}

/** Native styled select — same visual language as the app's filter dropdowns. */
const Select = React.forwardRef<HTMLSelectElement, SelectProps>(
  ({ className, children, ...props }, ref) => (
    <div className="relative w-full">
      <select
        ref={ref}
        className={cn(
          "flex h-8 w-full appearance-none rounded-md border border-border/50 bg-input px-3 pr-8 py-1.5 text-input-field shadow-sm ring-offset-background focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring/70 focus-visible:border-ring/70 disabled:cursor-not-allowed disabled:opacity-50 transition-all duration-150 ease-out",
          className
        )}
        {...props}
      >
        {children}
      </select>
      <ChevronDown className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
    </div>
  )
)
Select.displayName = "Select"

export { Select }
